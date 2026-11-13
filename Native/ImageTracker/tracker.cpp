// Android/desktop image-only replacement for the obsolete QCAR platform plugin.
// Gameplay remains the recovered 2.6 implementation. No SLAM, world anchor or pose prediction.
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>
#include <opencv2/features2d.hpp>
#include <opencv2/calib3d.hpp>
#include <opencv2/video/tracking.hpp>
#include <vector>
#include <algorithm>
#include <cmath>
#include <cstring>
#include <mutex>

#ifdef _WIN32
#define API extern "C" __declspec(dllexport)
#else
#define API extern "C" __attribute__((visibility("default")))
#endif
using namespace cv;
struct Target {
    Mat grey, descriptors, check;
    Ptr<DescriptorMatcher> matcher;
    std::vector<Point2f> points;
    float width;
};
struct ImageTracker {
    Ptr<SIFT> sift=SIFT::create(2400,3,.025,10,1.6);
    std::vector<Target> targets;
    Mat previous;
    std::vector<Point2f> objectPoints, framePoints;
    int active=-1, age=0;
};
static Mat greyRGBA(const unsigned char* rgba,int w,int h,bool bottomUp) {
    Mat grey; cvtColor(Mat(h,w,CV_8UC4,const_cast<unsigned char*>(rgba)),grey,COLOR_RGBA2GRAY);
    if(bottomUp) flip(grey,grey,0);
    return grey;
}
API void* cft_create() { try { static std::once_flag configured;std::call_once(configured,[]{setNumThreads(2);});return new ImageTracker(); } catch(...) { return nullptr; } }
API void cft_destroy(void* handle) { delete static_cast<ImageTracker*>(handle); }
API int cft_add_target(void* handle,const unsigned char* rgba,int w,int h,float width) {
    try {
        auto& tracker=*static_cast<ImageTracker*>(handle);
        Target t; t.grey=greyRGBA(rgba,w,h,true); t.width=width;
        resize(t.grey,t.check,Size(128,128),0,0,INTER_AREA);
        // Matching small camera images against equally small reference octaves avoids
        // requiring the target to occupy a fixed percentage of the camera frame.
        for(int size: {128,192,288,432,640})for(Point2f squash: {Point2f(1,1),Point2f(1,.6f),Point2f(.6f,1)}) {
            Mat scaled,desc; float scale=float(size)/std::max(w,h);
            resize(t.grey,scaled,Size(),scale*squash.x,scale*squash.y,INTER_AREA);
            std::vector<KeyPoint> keys; tracker.sift->detectAndCompute(scaled,noArray(),keys,desc);
            if(desc.empty()) continue;
            for(auto& k: keys)t.points.emplace_back(k.pt.x/(scale*squash.x),k.pt.y/(scale*squash.y));
            t.descriptors.push_back(desc);
        }
        if(t.points.size()<30)return -1;
        t.matcher=makePtr<FlannBasedMatcher>(makePtr<flann::KDTreeIndexParams>(4),makePtr<flann::SearchParams>(64));
        t.matcher->add(std::vector<Mat>{t.descriptors});t.matcher->train();
        tracker.targets.push_back(t);return int(tracker.targets.size())-1;
    } catch(...) { return -1; }
}
static double coverage(const std::vector<Point2f>& points,double area) {
    if(points.size()<3)return 0;
    std::vector<Point2f> hull;convexHull(points,hull);return contourArea(hull)/area;
}
static bool verify(const Target& t,const Mat& grey,const Mat& H,std::vector<Point2f>& corners) {
    std::vector<Point2f> original={{0,0},{float(t.grey.cols),0},{float(t.grey.cols),float(t.grey.rows)},{0,float(t.grey.rows)}};
    perspectiveTransform(original,corners,H);
    if(!isContourConvex(corners) || contourArea(corners,true)<900)return false;
    for(int i=0;i<4;i++) {
        if(norm(corners[i]-corners[(i+1)%4])<24)return false;
        if(corners[i].x < -grey.cols*.35 || corners[i].x>grey.cols*1.35 || corners[i].y < -grey.rows*.35 || corners[i].y>grey.rows*1.35)return false;
    }
    // Current-frame appearance validation prevents optical flow following the table
    // or a hand after the card has been removed. Tile correlation tolerates exposure
    // changes and partial occlusion without accepting an invisible card.
    Mat scale=(Mat_<double>(3,3)<<128./t.grey.cols,0,0,0,128./t.grey.rows,0,0,0,1), warped;
    warpPerspective(grey,warped,scale*H.inv(),Size(128,128),INTER_LINEAR,BORDER_CONSTANT,Scalar(0));
    int good=0;
    for(int y=0;y<4;y++)for(int x=0;x<4;x++) {
        Rect r(x*32,y*32,32,32); Mat a,b;
        warped(r).convertTo(a,CV_32F);t.check(r).convertTo(b,CV_32F);
        Scalar ma,sa,mb,sb;meanStdDev(a,ma,sa);meanStdDev(b,mb,sb);
        if(sa[0]<5 || sb[0]<5)continue;
        a-=ma[0];b-=mb[0];double corr=a.dot(b)/(1024*sa[0]*sb[0]);
        if(corr>.50)good++;
    }
    return good>=6;
}
// output[0]=target (-1 on loss), [1]=inliers, [2]=RMS pixels, [3..11]=CV rotation
// [12..14]=CV translation (metres), [15..22]=upright image corners.
// Input is Unity Color32 (bottom-up). rotation is clockwise WebCamTexture rotation.
API int cft_process(void* handle,const unsigned char* rgba,int w,int h,int rotation,int mirrored,
                    float fx,float fy,float cx,float cy,float* output) {
    std::fill(output,output+24,0.f);output[0]=-1;
    try {
        auto& tr=*static_cast<ImageTracker*>(handle);
        Mat raw=greyRGBA(rgba,w,h,!mirrored), grey;
        rotation=(rotation%360+360)%360;
        if(rotation==90)rotate(raw,grey,ROTATE_90_CLOCKWISE);
        else if(rotation==180)rotate(raw,grey,ROTATE_180);
        else if(rotation==270)rotate(raw,grey,ROTATE_90_COUNTERCLOCKWISE);
        else grey=raw;
        double factor=std::min(1.,1280./std::max(grey.cols,grey.rows));
        if(factor<1)resize(grey,grey,Size(),factor,factor,INTER_AREA);
        Mat H;std::vector<Point2f> obj,img,corners;
        int selected=-1;
        // Forward/backward flow observes the image again on every processed frame.
        // Periodic descriptor matching refreshes points and bounds accumulated drift.
        if(tr.active>=0 && !tr.previous.empty() && tr.previous.size()==grey.size() && tr.age<12) {
            std::vector<Point2f> next,back;std::vector<uchar> ok,reverse;std::vector<float> error;
            calcOpticalFlowPyrLK(tr.previous,grey,tr.framePoints,next,ok,error,Size(21,21),3);
            calcOpticalFlowPyrLK(grey,tr.previous,next,back,reverse,error,Size(21,21),3);
            for(size_t i=0;i<next.size();i++)if(ok[i] && reverse[i] && norm(back[i]-tr.framePoints[i])<1.2 && next[i].x>=0 && next[i].y>=0 && next[i].x<grey.cols && next[i].y<grey.rows) {
                obj.push_back(tr.objectPoints[i]);img.push_back(next[i]);
            }
            selected=tr.active;
        }
        for(int attempt=0;attempt<2;attempt++) {
            if(attempt==1 || selected<0 || obj.size()<10) {
                std::vector<KeyPoint> keys;Mat desc;tr.sift->detectAndCompute(grey,noArray(),keys,desc);
                selected=-1;obj.clear();img.clear();int best=0;
                if(!desc.empty())for(int ti=0;ti<int(tr.targets.size());ti++) {
                    const Target& t=tr.targets[ti];std::vector<std::vector<DMatch>> matches;
                    // Query each frame keypoint once; reference pyramids cannot duplicate an image point.
                    t.matcher->knnMatch(desc,matches,2);
                    std::vector<Point2f> a,b;
                    for(auto& m:matches)if(m.size()==2 && m[0].distance<.76f*m[1].distance) {
                        a.push_back(t.points[m[0].trainIdx]);b.push_back(keys[m[0].queryIdx].pt);
                    }
                    if(a.size()<10)continue;
                    Mat mask,h=findHomography(a,b,RANSAC,2.5,mask,1500,.995);
                    if(h.empty())continue;
                    int count=countNonZero(mask);
                    if(count>best && verify(t,grey,h,corners)) { best=count;obj=a;img=b;selected=ti; }
                }
                tr.age=0;
            }
            if(selected<0 || obj.size()<10)break;
            Mat mask;H=findHomography(obj,img,RANSAC,2.5,mask,1500,.995);
            if(H.empty())continue;
            std::vector<Point2f> a,b;
            for(size_t i=0;i<obj.size();i++)if(mask.at<uchar>(int(i))) { a.push_back(obj[i]);b.push_back(img[i]); }
            obj=a;img=b;const Target& t=tr.targets[selected];
            if(obj.size()<10 || coverage(obj,t.grey.total())<.06 || !verify(t,grey,H,corners)) { selected=-1;continue; }
            // Coordinates: +X right, +Y card normal, +Z down printed image.
            std::vector<Point3f> world;
            for(auto& p:obj)world.emplace_back((p.x/t.grey.cols-.5f)*t.width,0.f,(p.y/t.grey.rows-.5f)*t.width*t.grey.rows/t.grey.cols);
            Mat K=(Mat_<double>(3,3)<<fx*factor,0,cx*factor,0,fy*factor,cy*factor,0,0,1);
            Mat rv,tv,R;
            if(!solvePnP(world,img,K,noArray(),rv,tv,false,SOLVEPNP_ITERATIVE) || tv.at<double>(2)<=0) { selected=-1;continue; }
            std::vector<Point2f> projected;projectPoints(world,rv,tv,K,noArray(),projected);
            double rms=0;for(size_t i=0;i<img.size();i++)rms+=norm(projected[i]-img[i])*norm(projected[i]-img[i]);
            rms=std::sqrt(rms/img.size())/factor;
            if(rms>4) { selected=-1;continue; }
            Rodrigues(rv,R);output[0]=float(selected);output[1]=float(obj.size());output[2]=float(rms);
            for(int i=0;i<9;i++)output[3+i]=float(R.at<double>(i/3,i%3));
            for(int i=0;i<3;i++)output[12+i]=float(tv.at<double>(i));
            for(int i=0;i<4;i++) { output[15+2*i]=corners[i].x/float(factor);output[16+2*i]=corners[i].y/float(factor); }
            tr.active=selected;tr.objectPoints=obj;tr.framePoints=img;tr.previous=grey;tr.age++;
            return 1;
        }
        tr.active=-1;tr.previous.release();tr.objectPoints.clear();tr.framePoints.clear();return 0;
    } catch(...) { static_cast<ImageTracker*>(handle)->active=-1;return -1; }
}
