using System.IO;

namespace UltimateReplay.Core
{
    internal interface IReplayDataSerialize
    {
        // Methods
        void OnReplayDataSerialize(BinaryWriter writer);

        void OnReplayDataDeserialize(BinaryReader reader);
    }
}
