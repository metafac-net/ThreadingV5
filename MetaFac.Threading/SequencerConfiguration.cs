using System;
using System.Linq;

namespace MetaFac.Threading
{
    public class SequencerConfiguration : ISequencerConfiguration
    {
        private int[] _levelWidths = new int[] { 16, 8 };

        public int[] LevelWidths
        {
            get => _levelWidths.ToArray();
            set => _levelWidths = value?.ToArray() ?? Array.Empty<int>();
        }

        public SequencerConfiguration() { }

        public SequencerConfiguration(ISequencerConfiguration source)
        {
            LevelWidths = source.LevelWidths;
        }
    }
}
