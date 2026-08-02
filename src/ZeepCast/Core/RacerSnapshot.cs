using UnityEngine;
using ZeepkistClient;

namespace ZeepCast.Core
{
    internal enum RacerStatusKind
    {
        Racing,
        Finished,
        Crashed,
        Spectating,
        Damaged,
        Boosting,
        Fanning,
        Braking
    }

    /// <summary>
    /// One read-only projection of a network racer for the camera and UI.
    /// SteamId is identity; Name is display text only.
    /// </summary>
    internal sealed class RacerSnapshot
    {
        public RacerSnapshot(
            ulong steamId,
            int position,
            string name,
            Color color,
            ZeepkistNetworkPlayer player,
            NetworkedZeepkistGhost ghost,
            Transform transform,
            int checkpoints,
            float runtime,
            int speed,
            int championshipPoints,
            bool finished,
            float finishTime,
            RacerStatusKind statusKind,
            string status)
        {
            SteamId = steamId;
            Position = position;
            Name = name;
            Color = color;
            Player = player;
            Ghost = ghost;
            Transform = transform;
            Checkpoints = checkpoints;
            Runtime = runtime;
            Speed = speed;
            ChampionshipPoints = championshipPoints;
            Finished = finished;
            FinishTime = finishTime;
            StatusKind = statusKind;
            Status = status;
        }

        public ulong SteamId { get; }
        public int Position { get; }
        public string Name { get; }
        public Color Color { get; }
        public ZeepkistNetworkPlayer Player { get; }
        public NetworkedZeepkistGhost Ghost { get; }
        public Transform Transform { get; }
        public int Checkpoints { get; }
        public float Runtime { get; }
        public int Speed { get; }
        public int ChampionshipPoints { get; }
        public bool Finished { get; }
        public float FinishTime { get; }
        public RacerStatusKind StatusKind { get; }
        public string Status { get; }
        public float DisplayTime => Finished ? FinishTime : Runtime;

        public RacerSnapshot WithPosition(int position)
        {
            return new RacerSnapshot(
                SteamId,
                position,
                Name,
                Color,
                Player,
                Ghost,
                Transform,
                Checkpoints,
                Runtime,
                Speed,
                ChampionshipPoints,
                Finished,
                FinishTime,
                StatusKind,
                Status);
        }
    }

    internal readonly struct BroadcastFieldSummary
    {
        public BroadcastFieldSummary(
            int total,
            int racing,
            int finished,
            int spectating,
            int incidents)
        {
            Total = total;
            Racing = racing;
            Finished = finished;
            Spectating = spectating;
            Incidents = incidents;
        }

        public int Total { get; }
        public int Racing { get; }
        public int Finished { get; }
        public int Spectating { get; }
        public int Incidents { get; }
    }
}
