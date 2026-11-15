using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CandyFlick.Multiplayer
{
    // Bounded length-prefixed TCP frames; sockets are nonblocking and pumped on
    // the main thread, including while gameplay timeScale is zero.
    public sealed class LanPeer : IDisposable
    {
        const int MaxFrame=65536,MaxQueued=524288;
        readonly Socket socket;readonly TcpClient clientOwner;readonly byte[] input=new byte[MaxFrame+4];int used;
        readonly Queue<byte[]> output=new Queue<byte[]>();int outputOffset,queued;
        public int Id=-1;public double LastSeen;public readonly double OpenedAt;public bool Closed {get;private set;}
        public readonly Queue<string> Messages=new Queue<string>();
        public string Address=>((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
        public LanPeer(Socket connection,double now)
        {
            socket=connection;socket.Blocking=false;socket.NoDelay=true;LastSeen=OpenedAt=now;
        }
        public LanPeer(TcpClient client,double now):this(client.Client,now){clientOwner=client;}
        public void Send(LanMessage message)=>SendText(JsonUtility.ToJson(message));
        public void SendText(string text)
        {
            if(Closed)return;byte[] data=Encoding.UTF8.GetBytes(text);
            if(data.Length==0||data.Length>MaxFrame||queued+data.Length+4>MaxQueued){Dispose();return;}
            byte[] frame=new byte[data.Length+4];int size=data.Length;
            for(int i=0;i<4;i++)frame[i]=(byte)(size>>(8*i));Buffer.BlockCopy(data,0,frame,4,size);output.Enqueue(frame);queued+=frame.Length;
        }
        public void Pump(double now)
        {
            if(Closed)return;
            try
            {
                int budget=131072;
                while(budget>0&&socket.Poll(0,SelectMode.SelectRead))
                {
                    if(used==input.Length){Dispose();return;}
                    int count=socket.Receive(input,used,Math.Min(input.Length-used,budget),SocketFlags.None);
                    if(count==0){Dispose();return;}used+=count;budget-=count;LastSeen=now;
                    while(used>=4)
                    {
                        int length=input[0]|input[1]<<8|input[2]<<16|input[3]<<24;
                        if(length<=0||length>MaxFrame){Dispose();return;}if(used<length+4)break;
                        if(Messages.Count>=128){Dispose();return;}
                        Messages.Enqueue(Encoding.UTF8.GetString(input,4,length));used-=length+4;
                        Buffer.BlockCopy(input,length+4,input,0,used);
                    }
                }
                budget=131072;
                while(output.Count>0&&budget>0&&socket.Poll(0,SelectMode.SelectWrite))
                {
                    byte[] frame=output.Peek();int count=socket.Send(frame,outputOffset,Math.Min(frame.Length-outputOffset,budget),SocketFlags.None);
                    if(count<=0){Dispose();return;}outputOffset+=count;budget-=count;queued-=count;
                    if(outputOffset==frame.Length){output.Dequeue();outputOffset=0;}
                }
            }
            catch(SocketException e){if(e.SocketErrorCode!=SocketError.WouldBlock&&e.SocketErrorCode!=SocketError.IOPending)Dispose();}
            catch(ObjectDisposedException){Dispose();}
        }
        public void Dispose(){if(Closed)return;Closed=true;try{socket.Dispose();clientOwner?.Dispose();}catch{} output.Clear();}
    }
    public sealed class LanTransport : IDisposable
    {
        public const int DiscoveryPort=47831,GamePort=47832;
        static readonly IPAddress Group=IPAddress.Parse("239.255.42.99");
        readonly Dictionary<string,LanRoom> rooms=new Dictionary<string,LanRoom>();
        readonly string roomId=Guid.NewGuid().ToString("N");
        public readonly List<LanPeer> Peers=new List<LanPeer>();
        TcpListener listener;UdpClient discovery;TcpClient pendingClient;Task connectTask;
        double connectStarted,nextAdvertisement;public bool IsHost=>listener!=null;
        public LanPeer Server {get;private set;}
        public bool Connecting=>connectTask!=null;
        public string Error {get;private set;}="";
        public int Port=>listener==null?0:((IPEndPoint)listener.LocalEndpoint).Port;
        public IReadOnlyList<LanRoom> Rooms=>rooms.Values.OrderBy(r=>r.name).ToArray();
        public void Browse()
        {
            if(discovery!=null)return;
            try
            {
                discovery=new UdpClient(AddressFamily.InterNetwork);
                discovery.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);
                discovery.Client.Bind(new IPEndPoint(IPAddress.Any,DiscoveryPort));
                discovery.EnableBroadcast=true;discovery.MulticastLoopback=true;discovery.Ttl=1;
                discovery.JoinMulticastGroup(Group);discovery.Client.Blocking=false;
            }
            catch(Exception){discovery?.Dispose();discovery=null;Error="Room discovery is unavailable. You can still join by address.";}
        }
        public bool Host(int port=GamePort)
        {
            try{listener=new TcpListener(IPAddress.Any,port);listener.Start(16);listener.Server.Blocking=false;Browse();Error="";return true;}
            catch(SocketException){listener?.Stop();listener=null;Error="Could not open a room on this device.";return false;}
        }
        public bool Join(string address,double now)
        {
            if(Connecting||Server!=null)return false;
            string[] parts=(address??"").Trim().Split(':');int port=GamePort;
            if(parts.Length>2||!IPAddress.TryParse(parts[0],out var ip)||ip.AddressFamily!=AddressFamily.InterNetwork||
                (parts.Length==2&&(!int.TryParse(parts[1],out port)||port<1||port>65535)))
            {Error="Enter a local address such as 192.168.1.5.";return false;}
            try{pendingClient=new TcpClient(AddressFamily.InterNetwork);connectStarted=now;connectTask=pendingClient.ConnectAsync(ip,port);Error="";return true;}
            catch(Exception){pendingClient?.Dispose();pendingClient=null;connectTask=null;Error="Could not connect to this room.";return false;}
        }
        public void Advertise(string name,int count,bool lobby,double now)
        {
            if(!IsHost||!lobby||discovery==null||now<nextAdvertisement)return;nextAdvertisement=now+1;
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LanRoom{name=LanRules.CleanName(name),roomId=roomId,port=Port,count=count}));
            SendAdvertisement(bytes,new IPEndPoint(Group,DiscoveryPort));
            SendAdvertisement(bytes,new IPEndPoint(IPAddress.Broadcast,DiscoveryPort));
            // Directed broadcast also works on routers that filter limited broadcasts.
            try{foreach(var network in NetworkInterface.GetAllNetworkInterfaces())foreach(var unicast in network.GetIPProperties().UnicastAddresses)
            {
                if(unicast.Address.AddressFamily!=AddressFamily.InterNetwork||unicast.IPv4Mask==null||IPAddress.IsLoopback(unicast.Address))continue;
                byte[] ip=unicast.Address.GetAddressBytes(),mask=unicast.IPv4Mask.GetAddressBytes();
                for(int i=0;i<4;i++)ip[i]=(byte)(ip[i]|~mask[i]);SendAdvertisement(bytes,new IPEndPoint(new IPAddress(ip),DiscoveryPort));
            }}catch{}
        }
        void SendAdvertisement(byte[] bytes,IPEndPoint end){try{discovery.Send(bytes,bytes.Length,end);}catch(SocketException){}}
        public void Pump(double now)
        {
            if(connectTask!=null)
            {
                if(connectTask.IsCompleted)
                {
                    if(connectTask.IsFaulted||connectTask.IsCanceled){var observed=connectTask.Exception;pendingClient.Dispose();Error="Could not connect to this room.";}
                    else{Server=new LanPeer(pendingClient,now);pendingClient=null;}
                    connectTask=null;
                }
                else if(now-connectStarted>5){pendingClient.Dispose();connectTask=null;pendingClient=null;Error="Connection timed out. Check the address and Wi-Fi.";}
            }
            if(listener!=null)
            {
                try{while(listener.Pending())
                {
                    var socket=listener.AcceptSocket();if(Peers.Count>=LanRules.MaxPlayers+4)socket.Dispose();else Peers.Add(new LanPeer(socket,now));
                }}catch(SocketException){}
            }
            foreach(var peer in Peers)peer.Pump(now);Server?.Pump(now);
            if(discovery!=null)
            {
                try{int budget=32;while(budget-->0&&discovery.Available>0)
                {
                    var source=new IPEndPoint(IPAddress.Any,0);byte[] data=discovery.Receive(ref source);if(data.Length>2048)continue;
                    LanRoom room;try{room=JsonUtility.FromJson<LanRoom>(Encoding.UTF8.GetString(data));}catch{continue;}
                    if(room==null||room.magic!="CandyFlickLAN"||room.version!=LanRules.Version||string.IsNullOrEmpty(room.roomId)||room.roomId==roomId||room.port<1||room.port>65535||room.count<1||room.count>LanRules.MaxPlayers)continue;
                    room.name=LanRules.CleanName(room.name);room.address=source.Address+":"+room.port;room.seen=now;
                    if(rooms.Count<64||rooms.ContainsKey(room.roomId))rooms[room.roomId]=room;
                }}catch(SocketException){}catch(ObjectDisposedException){}
            }
            foreach(string id in rooms.Where(p=>now-p.Value.seen>4).Select(p=>p.Key).ToArray())rooms.Remove(id);
        }
        public void Broadcast(LanMessage message){string json=JsonUtility.ToJson(message);foreach(var p in Peers)if(p.Id>=1)p.SendText(json);}
        public static string LocalAddress()
        {
            try{return NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up)
                .OrderByDescending(n=>n.GetIPProperties().GatewayAddresses.Any(g=>g.Address.AddressFamily==AddressFamily.InterNetwork&&!g.Address.Equals(IPAddress.Any)))
                .ThenByDescending(n=>n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211)
                .SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address))
                .Select(a=>a.Address.ToString()).FirstOrDefault()??"127.0.0.1";}catch{return "127.0.0.1";}
        }
        public void Dispose()
        {
            listener?.Stop();listener=null;discovery?.Dispose();discovery=null;pendingClient?.Dispose();pendingClient=null;
            if(connectTask!=null&&connectTask.IsFaulted){var observed=connectTask.Exception;}connectTask=null;
            foreach(var p in Peers)p.Dispose();Peers.Clear();Server?.Dispose();Server=null;rooms.Clear();
        }
    }
}
