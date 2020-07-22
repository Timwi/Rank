using System;
using System.Collections.Generic;
using RT.Serialization;

namespace Timwi.Rank
{
    sealed class RankRankingSlim
    {
        public string PublicToken;
        public string Title;
        public bool Finished;
    }

    struct RankComparison : IEquatable<RankComparison>
    {
        public int Less { get; private set; }
        public int More { get; private set; }

        public RankComparison(int less, int more) { Less = less; More = more; }

        public bool Equals(RankComparison other) => other.Less == Less && other.More == More;
        public override int GetHashCode() => Less * 257 + More;
    }

    sealed class RankRanking
    {
        public string SetHash;
        public string PublicToken;
        public string PrivateToken;
        public string Title;
        public string Question;
        public bool Finished;

        [ClassifyNotNull]
        public HashSet<RankComparison> Comparisons = new HashSet<RankComparison>();
        [ClassifyNotNull]
        public List<int> Ignore = new List<int>();

        public RankRankingSlim ToSlim() => new RankRankingSlim { Finished = Finished, PublicToken = PublicToken, Title = Title };
    }
}
