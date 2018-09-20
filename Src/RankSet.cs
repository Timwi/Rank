using System.Collections.Generic;
using RT.Util;
using RT.Util.Collections;
using RT.Util.Serialization;

namespace Timwi.Rank
{
    sealed class RankSetSlim
    {
        public string Hash;
        public string Name;
    }

    sealed class RankSet
    {
        public string Hash;
        public string Name;
        public string[] Items;

        [ClassifyNotNull]
        public Dictionary<string, RankRankingSlim> DicRankings = new Dictionary<string, RankRankingSlim>();
        [ClassifyNotNull]
        public ListSorted<RankRankingSlim> ListRankings = new ListSorted<RankRankingSlim>(CustomComparer<RankRankingSlim>.By(r => !r.Finished).ThenBy(r => r.Title));

        public RankSetSlim ToSlim() => new RankSetSlim { Hash = Hash, Name = Name };
    }
}
