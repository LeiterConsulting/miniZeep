using UnityEngine;
using ZeepkistClient;

namespace ZeepCast.Core
{
    internal sealed class RacerSnapshot
    {
        public ulong SteamId { get; set; }
        public int Position { get; set; }
        public string Name { get; set; } = string.Empty;
        public Color Color { get; set; }
        public ZeepkistNetworkPlayer Player { get; set; } = null!;
        public NetworkedZeepkistGhost Ghost { get; set; } = null!;
        public Transform Transform { get; set; } = null!;
        public int Checkpoints { get; set; }
        public float Runtime { get; set; }
        public int Speed { get; set; }
        public bool Finished { get; set; }
        public float FinishTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
