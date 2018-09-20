using System.Collections.Generic;
using RT.Util.Serialization;

namespace Timwi.Rank
{
    sealed class RankRankingSlim
    {
        public string PublicToken;
        public string Title;
        public bool Finished;
    }

    sealed class RankComparison
    {
        public int Less;
        public int More;
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
        public List<RankComparison> Comparisons = new List<RankComparison>();
        [ClassifyNotNull]
        public List<int> Ignore = new List<int>();

        public RankRankingSlim ToSlim() => new RankRankingSlim { Finished = Finished, PublicToken = PublicToken, Title = Title };
    }
}
