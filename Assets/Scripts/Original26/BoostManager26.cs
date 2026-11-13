using UnityEngine;

namespace CandyFlick.Original26
{
    // BoostManager, 0x49e4c–0x4a630. 2.6 GetBoostTypeRandom always returns Vertical.
    public sealed class BoostManager26 : MonoBehaviour
    {
        public BoostV26 Current { get; private set; }
        CandyFlickGame game;
        bool boostEnabled;
        int round;
        public void Initialize(CandyFlickGame owner) { game = owner; }
        public void OnNewRound(int number)
        {
            if (round == number) return;
            round = number; boostEnabled = number > 2;
            CheckBoost();
        }
        public void Reset() { round = 0; boostEnabled = false; DeleteCurrentBoost(); }
        public void CheckBoost()
        {
            DeleteCurrentBoost();
            if (!boostEnabled || game.Session.IsOver) return;
            Vector3 position = GetBoostPositionRandom();
            GameObject instance = Instantiate(Resources.Load<GameObject>("Original26/BoostV"), game.World);
            Transform root = instance.transform.Find("boostv2");
            root.SetPositionAndRotation(position, Quaternion.identity);
            root.LookAt(game.Actor.transform);
            root.eulerAngles = new Vector3(0, root.eulerAngles.y, 0);
            Current = root.gameObject.AddComponent<BoostV26>();
            Current.Initialize(game, instance);
            game.RingCenters[0] = position;
        }
        void DeleteCurrentBoost()
        {
            if (Current) Current.DeleteWithAlphaTimer(.5f);
            Current = null;
        }
        static int GetMinusOrPlus() => new System.Random().Next(2) == 0 ? -1 : 1;
        Vector3 GetBoostPositionRandom()
        {
            var random = new System.Random();
            random.Next(520, 690); random.Next(520, 690);
            int y = random.Next(300, 500);
            GetMinusOrPlus(); GetMinusOrPlus();
            var direction = new GameObject("Original boost direction").transform;
            direction.position = game.Actor.transform.position;
            direction.LookAt(game.Player);
            direction.RotateAround(Vector3.zero, Vector3.down, new System.Random().Next(15) * GetMinusOrPlus());
            Vector3 position = direction.forward * random.Next(520, 690);
            position.y = y;
            Destroy(direction.gameObject);
            return position;
        }
    }
}
