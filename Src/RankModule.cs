using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RT.PostBuild;
using RT.PropellerApi;
using RT.Serialization;
using RT.Servers;
using RT.TagSoup;
using RT.Util;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;

namespace Timwi.Rank
{
    public sealed partial class RankModule : PropellerModuleBase<RankSettings>
    {
        public override string Name => "Rank";

        private Dictionary<string, RankSetSlim> _setsDic = new Dictionary<string, RankSetSlim>();
        private ListSorted<RankSetSlim> _setsList = new ListSorted<RankSetSlim>(CustomComparer<RankSetSlim>.By(rs => rs.Name, ignoreCase: true));

        private static void PostBuildCheck(IPostBuildReporter rep)
        {
        }

        public override void Init(LoggerBase log)
        {
            base.Init(log);

            if (Settings.DataDir != null && Directory.Exists(Settings.DataDir))
            {
                _setsList.Clear();
                _setsList.AddRange(new DirectoryInfo(Settings.DataDir).EnumerateFiles("set-*.json").Select(f => ClassifyJson.DeserializeFile<RankSet>(f.FullName).ToSlim()));
                _setsDic = _setsList.ToDictionary(set => set.Hash);
            }
        }

        public override HttpResponse Handle(HttpRequest req)
        {
            if (Settings.DataDir == null || !Directory.Exists(Settings.DataDir))
                return HttpResponse.Html($@"<h1>Data folder not configured.</h1>", HttpStatusCode._500_InternalServerError);

            if (req.Method == HttpMethod.Post)
                return processPost(req);

            if (req.Url["set"] != null)
            {
                var filePath = Path.Combine(Settings.DataDir, $"set-{req.Url["set"]}.json");
                if (!File.Exists(filePath))
                    return HttpResponse.PlainText("That set does not exist.", HttpStatusCode._404_NotFound);
                var set = ClassifyJson.DeserializeFile<RankSet>(filePath);

                return HttpResponse.Html(new HTML(
                    new HEAD(
                        new TITLE("Rank"),
                        new META { name = "viewport", content = "width=device-width,initial-scale=1.0" },
                        new META { charset = "utf-8" },
                        new STYLELiteral(Resources.RankCss)),
                    new BODY(
                        new H1(set.Name),
                        set.ListRankings.Count == 0 ? null : Ut.NewArray<object>(
                            new H2($"Existing rankings"),
                            new UL(set.ListRankings.Select(ranking => new LI(new A { href = req.Url.WithoutQuery().WithQuery("ranking", ranking.PublicToken).ToHref() }._(ranking.Title), ranking.Finished ? null : " (unfinished)")))),
                        new FORM { action = req.Url.ToHref(), method = method.post }._(
                            new INPUT { type = itype.hidden, name = "fnc", value = "start" },
                            new INPUT { type = itype.hidden, name = "set", value = set.Hash },
                            new H2($"Start a new ranking"),
                                new P("Enter the title for this ranking. Preferably start with your name and specify the ranking criterion. For example: “Brian, by preference” or “Thomas, by difficulty”."),
                                new DIV(new INPUT { name = "title", value = "Brian, by preference", accesskey = "," }),
                                new P("Enter the question to ask for comparing each pair of items (examples: “Which do you like better?” (ranking by preference), “Which do you find harder?” (ranking by difficulty), etc.)"),
                                new DIV(new INPUT { name = "question", value = "Which do you like better?" }),
                                new DIV(new BUTTON { type = btype.submit, accesskey = "g" }._(new KBD("G"), "o"))))));
            }

            if (req.Url["ranking"] != null)
            {
                var filePath = rankingPath(req.Url["ranking"]);
                if (!File.Exists(filePath))
                    return HttpResponse.PlainText("That ranking does not exist.", HttpStatusCode._404_NotFound);
                var ranking = ClassifyJson.DeserializeFile<RankRanking>(filePath);

                var setFilePath = setPath(ranking.SetHash);
                if (!File.Exists(setFilePath))
                    return HttpResponse.PlainText("The set does not exist.", HttpStatusCode._404_NotFound);
                var set = ClassifyJson.DeserializeFile<RankSet>(setFilePath);

                var canEdit = false;

                if (req.Url["secret"] != null)
                {
                    if (req.Url["secret"] != ranking.PrivateToken)
                        return HttpResponse.Redirect(req.Url.WithoutQuery("secret"));
                    canEdit = true;
                }

                var (ix1, ix2, ranked) = attemptRanking(ranking, set);

                return HttpResponse.Html(new HTML(
                    new HEAD(
                        new TITLE("Rank"),
                        new META { name = "viewport", content = "width=device-width,initial-scale=1.0" },
                        new META { charset = "utf-8" },
                        new STYLELiteral(Resources.RankCss)),
                    new BODY(
                        new H1(set.Name),
                        new H2(ranking.Title),
                        ix1 == -1 || !canEdit ? null : new FORM { action = req.Url.ToHref(), method = method.post }._(
                            new INPUT { type = itype.hidden, name = "fnc", value = "rank" },
                            new INPUT { type = itype.hidden, name = "ranking", value = ranking.PublicToken },
                            new INPUT { type = itype.hidden, name = "secret", value = ranking.PrivateToken },
                            new INPUT { type = itype.hidden, name = "ix1", value = ix1.ToString() },
                            new INPUT { type = itype.hidden, name = "ix2", value = ix2.ToString() },
                            new P { class_ = "comparison" }._(ranking.Question),
                            new DIV { class_ = "comparison" }._(new BUTTON { type = btype.submit, name = "more", value = ix1.ToString() }._(set.Items[ix1])),
                            new DIV { class_ = "comparison" }._(new BUTTON { type = btype.submit, name = "more", value = ix2.ToString() }._(set.Items[ix2]))),
                        new UL(ranked.Select(ix => new LI { class_ = ranked.All(rIx => rIx == ix || ranking.Comparisons.Any(rc => (rc.Less == ix && rc.More == rIx) || (rc.Less == rIx && rc.More == ix))) ? "complete" : "incomplete" }._(set.Items[ix]))))));
            }

            return HttpResponse.Html(new HTML(
                new HEAD(
                    new TITLE("Rank"),
                    new META { name = "viewport", content = "width=device-width,initial-scale=1.0" },
                    new META { charset = "utf-8" },
                    new STYLELiteral(Resources.RankCss)),
                new BODY(
                    new FORM { action = req.Url.ToHref(), method = method.post }._(
                        new INPUT { type = itype.hidden, name = "fnc", value = "create" },
                        new H1("Rank what?"),
                        new P("Choose a set to rank."),
                        new UL(_setsList.Select(s => new LI(new A { href = req.Url.WithQuery("set", s.Hash).ToHref() }._(s.Name)))),
                        new P("Or enter/paste the items that need ranking (one item per line)."),
                        new DIV(new TEXTAREA { name = "items", accesskey = "," }),
                        new P("Give it a name (e.g.: Episodes of “Best Show Evar”)."),
                        new DIV(new INPUT { type = itype.text, name = "title", value = "Episodes of “Best Show Evar”" }),
                        new DIV(new BUTTON { type = btype.submit, accesskey = "g" }._(new KBD("G"), "o"))))));
        }

        private static (int ix1, int ix2, int[] ranked) attemptRanking(RankRanking ranking, RankSet set)
        {
            var ix1 = -1;
            var ix2 = -1;
            var indexesToRank = Enumerable.Range(0, set.Items.Length).Except(ranking.Ignore).ToArray();
            var ranked = indexesToRank.OrderBy((i1, i2) =>
            {
                if (i1 == i2)
                    return 0;
                if (ranking.Comparisons.Contains(new RankComparison(i1, i2)))
                    return 1;
                if (ranking.Comparisons.Contains(new RankComparison(i2, i1)))
                    return -1;

                if (ix1 == -1)
                {
                    ix1 = i1;
                    ix2 = i2;
                }
                return 0;
            }).ToArray();
            return (ix1, ix2, ranked);
        }

        private HttpResponse processPost(HttpRequest req)
        {
            switch (req.Post["fnc"].Value)
            {
                case "create":
                    var items = req.Post["items"].Value.Replace("\r", "").Trim().Split('\n').Select(line => line.Trim()).ToArray();
                    var hash = MD5.Create().ComputeHash(items.JoinString("\n").ToUtf8()).ToHex();
                    var newSet = new RankSet { Hash = hash, Items = items, Name = req.Post["title"].Value };
                    if (!_setsDic.ContainsKey(hash))
                        lock (this)
                            if (!_setsDic.ContainsKey(hash))
                            {
                                var newSetSlim = newSet.ToSlim();
                                _setsDic[hash] = newSetSlim;
                                _setsList.Add(newSetSlim);
                                ClassifyJson.SerializeToFile(newSet, setPath(hash));
                            }
                    return HttpResponse.Redirect(req.Url.WithQuery("set", hash));

                case "start":
                    lock (this)
                    {
                        var currentSetHash = req.Post["set"].Value;
                        var setFilePath = setPath(currentSetHash);
                        if (!File.Exists(setFilePath))
                            return HttpResponse.PlainText("That set does not exist.", HttpStatusCode._404_NotFound);
                        var currentSet = ClassifyJson.DeserializeFile<RankSet>(setFilePath);

                        retry:
                        var publicToken = Rnd.GenerateString(32);
                        var privateToken = Rnd.GenerateString(32);
                        var path = rankingPath(publicToken);
                        if (File.Exists(path) || currentSet.DicRankings.ContainsKey(publicToken))
                            goto retry;
                        var newRanking = new RankRanking { Finished = false, PublicToken = publicToken, PrivateToken = privateToken, SetHash = currentSetHash, Title = req.Post["title"].Value, Question = req.Post["question"].Value };
                        var newRankingSlim = newRanking.ToSlim();
                        currentSet.DicRankings[publicToken] = newRankingSlim;
                        currentSet.ListRankings.Add(newRankingSlim);
                        ClassifyJson.SerializeToFile(newRanking, path);
                        ClassifyJson.SerializeToFile(currentSet, setFilePath);
                        return HttpResponse.Redirect(req.Url.WithoutQuery().WithQuery("ranking", publicToken).WithQuery("secret", privateToken));
                    }

                case "rank":
                    lock (this)
                    {
                        var publicToken = req.Post["ranking"].Value;
                        var privateToken = req.Post["secret"].Value;
                        var rankingFilePath = rankingPath(publicToken);
                        if (!File.Exists(rankingFilePath))
                            return HttpResponse.PlainText("That ranking does not exist.", HttpStatusCode._404_NotFound);
                        var currentRanking = ClassifyJson.DeserializeFile<RankRanking>(rankingFilePath);
                        if (privateToken != currentRanking.PrivateToken)
                            return HttpResponse.PlainText("You cannot vote in this ranking.", HttpStatusCode._404_NotFound);

                        var setFilePath = setPath(currentRanking.SetHash);
                        if (!File.Exists(setFilePath))
                            return HttpResponse.PlainText("That set does not exist.", HttpStatusCode._404_NotFound);
                        var currentSet = ClassifyJson.DeserializeFile<RankSet>(setFilePath);

                        if (!int.TryParse(req.Post["ix1"].Value, out var ix1) || !int.TryParse(req.Post["ix2"].Value, out var ix2) || !int.TryParse(req.Post["more"].Value, out var more) || (more != ix1 && more != ix2))
                            return HttpResponse.PlainText("Invalid integers.", HttpStatusCode._404_NotFound);

                        var newComparison = new RankComparison(more == ix1 ? ix2 : ix1, more == ix1 ? ix1 : ix2);

                        // Transitive closure
                        var ancestorLesses = currentRanking.Comparisons.Where(c => c.More == newComparison.Less).Select(c => c.Less).ToList();
                        ancestorLesses.Add(newComparison.Less);
                        var descendantMores = currentRanking.Comparisons.Where(c => c.Less == newComparison.More).Select(c => c.More).ToList();
                        descendantMores.Add(newComparison.More);
                        for (int i = 0; i < ancestorLesses.Count; i++)
                            for (int j = 0; j < descendantMores.Count; j++)
                                currentRanking.Comparisons.Add(new RankComparison(ancestorLesses[i], descendantMores[j]));

                        var result = attemptRanking(currentRanking, currentSet);
                        if (result.ix1 == -1)
                        {
                            currentRanking.Finished = true;

                            // This relies on reference equality, i.e. that currentSet.ListRankings contains the same object
                            currentSet.DicRankings[publicToken].Finished = true;

                            ClassifyJson.SerializeToFile(currentSet, setFilePath);
                        }
                        ClassifyJson.SerializeToFile(currentRanking, rankingFilePath);

                        return HttpResponse.Redirect(req.Url.WithoutQuery().WithQuery("ranking", publicToken).WithQuery("secret", privateToken));
                    }
            }

            return HttpResponse.PlainText("What?", HttpStatusCode._500_InternalServerError);
        }

        private string setPath(string hash) => Path.Combine(Settings.DataDir, $"set-{hash}.json");
        private string rankingPath(string publicToken) => Path.Combine(Settings.DataDir, $"ranking-{publicToken}.json");
    }
}