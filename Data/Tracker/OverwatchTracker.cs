using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using MopsBot.Data.Tracker.APIResults;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MopsBot.Data.Tracker.APIResults.Overwatch;

namespace MopsBot.Data.Tracker
{
    /// <summary>
    /// A tracker which keeps track of an Overwatch players stats
    /// </summary>
    public class OverwatchTracker : ITracker
    {
        private OStatsResult information;

        /// <summary>
        /// Initialises the tracker by setting attributes and setting up a Timer with a 10 minutes interval
        /// </summary>
        /// <param Name="OWName"> The Name-Battletag combination of the player to track </param>
        public OverwatchTracker() : base(600000, ExistingTrackers * 20000)
        {
        }

        public OverwatchTracker(string OWName) : base(60000)
        {
            Name = OWName;

            //Check if person exists by forcing Exceptions if not.
            try
            {
                var checkExists = overwatchInformation().Result;
                var test = checkExists.eu;
            }
            catch (Exception)
            {
                Dispose();
                throw new Exception($"Player {TrackerUrl()} could not be found on Overwatch!\nPerhaps the profile is private?");
            }
        }

        /// <summary>
        /// Event for the Timer, to check for changed stats
        /// </summary>
        /// <param Name="stateinfo"></param>
        protected async override void CheckForChange_Elapsed(object stateinfo)
        {
            try
            {
                OStatsResult newInformation = await overwatchInformation();

                if (information == null)
                {
                    information = newInformation;
                }

                if (newInformation == null) return;

                var changedStats = getChangedStats(information, newInformation);

                if (changedStats.Count != 0)
                {
                    foreach (ulong channel in ChannelIds.ToList())
                    {
                        await OnMajorChangeTracked(channel, createEmbed(newInformation, changedStats, getSessionMostPlayed(information.getNotNull().heroes.playtime, newInformation.getNotNull().heroes.playtime)), ChannelMessages[channel]);
                    }
                    information = newInformation;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" +  $"[ERROR] by {Name} at {DateTime.Now}:\n{e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Queries the API to fetch a JSON containing all the stats for the player
        /// Then converts it into OStatsResult
        /// </summary>
        /// <returns>An OStatsResult representing the fetched JSON as an object</returns>
        private async Task<OStatsResult> overwatchInformation()
        {
            string query = await MopsBot.Module.Information.ReadURLAsync($"http://localhost:4444/api/v3/u/{Name}/blob");

            JsonSerializerSettings _jsonWriter = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.DeserializeObject<OStatsResult>(query, _jsonWriter);
        }

        /// <summary>
        /// Queries the API to fetch a JSON containing all the stats for the player
        /// Then converts it into OStatsResult
        /// </summary>
        /// <returns>An OStatsResult representing the fetched JSON as an object</returns>
        public static async Task<Embed> overwatchInformation(string owName)
        {
            string query = await MopsBot.Module.Information.ReadURLAsync($"http://localhost:4444/api/v3/u/{owName}/blob");

            JsonSerializerSettings _jsonWriter = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            OStatsResult info = JsonConvert.DeserializeObject<OStatsResult>(query, _jsonWriter);
            Quickplay stats = info.getNotNull().stats.quickplay;
            var mostPlayed = getMostPlayed(info.getNotNull().heroes.playtime);

            EmbedBuilder e = new EmbedBuilder();
            e.Color = new Color(255, 152, 0);
            e.Title = "Stats";
            e.Url = $"https://playoverwatch.com/en-us/career/pc/eu/{owName}";

            EmbedAuthorBuilder author = new EmbedAuthorBuilder();
            author.Name = owName.Split("-")[0];
            author.Url = $"https://playoverwatch.com/en-us/career/pc/eu/{owName}";

            e.Author = author;

            EmbedFooterBuilder footer = new EmbedFooterBuilder();
            footer.IconUrl = "http://i.imgur.com/YZ4w2ey.png";
            footer.Text = "Overwatch";
            e.Timestamp = DateTime.Now;
            e.Footer = footer;

            e.ThumbnailUrl = stats.overall_stats.avatar;

            e.AddField("Damage", $"Total: {stats.game_stats.all_damage_done}" +
                            $"\nBest: {stats.game_stats.all_damage_done_most_in_game}" +
                            $"\nAverage: {stats.rolling_average_stats.all_damage_done * 1000}", true);

            e.AddField("Eliminations", $"Total: {stats.game_stats.eliminations}" +
                            $"\nKPD Best: {stats.game_stats.kill_streak_best}" +
                            $"\nKPD Average: {stats.game_stats.kpd}", true);

            e.AddField("Healing", $"Total: {stats.game_stats.healing_done}" +
                            $"\nBest: {stats.game_stats.healing_done_most_in_game}" +
                            $"\nAverage: {stats.rolling_average_stats.healing_done * 1000}", true);

            e.AddField("Medals", $"Bronze: {stats.game_stats.medals_bronze}" +
                            $"\nSilver: {stats.game_stats.medals_silver}" +
                            $"\nGold: {stats.game_stats.medals_gold}", true);

            e.AddField("General", $"Time played: {stats.game_stats.time_played}hrs" +
                            $"\nLevel: {stats.overall_stats.level + (100 * stats.overall_stats.prestige)}" +
                            $"\nWon Games: {stats.overall_stats.wins}" +
                            $"\n Endorsement Level: {stats.overall_stats.endorsement_level}", true);

            if(info.getNotNull().stats.competitive != null){
                author.IconUrl = info.getNotNull().stats.competitive.overall_stats.tier_image;
                e.AddField("Competitive", $"Time played: {info.getNotNull().stats.competitive.game_stats.time_played}hrs" +
                            $"\nWin Rate: {info.getNotNull().stats.competitive.overall_stats.win_rate}%" +
                            $"\nRank: {info.getNotNull().stats.competitive.overall_stats.comprank}", true);
            }


            e.AddField("Most Played", mostPlayed.Item2);

            if (mostPlayed.Item1.Equals("Ana") || mostPlayed.Item1.Equals("Moira") || mostPlayed.Item1.Equals("Orisa") || 
                mostPlayed.Item1.Equals("Doomfist") || mostPlayed.Item1.Equals("Sombra") || mostPlayed.Item1.Equals("Wrecking-Ball") ||
                mostPlayed.Item1.Equals("Brigitte"))
                e.ImageUrl = $"https://blzgdapipro-a.akamaihd.net/hero/{mostPlayed.Item1.ToLower()}/full-portrait.png";
            else
                e.ImageUrl = $"https://blzgdapipro-a.akamaihd.net/media/thumbnail/{mostPlayed.Item1.ToLower()}-gameplay.jpg";

            return e.Build();
        }

        ///<summary>Builds an embed out of the changed stats, and sends it as a Discord message </summary>
        /// <param Name="overwatchInformation">All fetched stats of the user </param>
        /// <param Name="changedStats">All changed stats of the user, together with a string presenting them </param>
        /// <param Name="mostPlayed">The most played Hero of the session, together with a string presenting them </param>
        private Embed createEmbed(OStatsResult overwatchInformation, Dictionary<string, string> changedStats, Tuple<string, string> mostPlayed)
        {
            OverallStats stats = overwatchInformation.getNotNull().stats.quickplay.overall_stats;

            EmbedBuilder e = new EmbedBuilder();
            e.Color = new Color(255, 152, 0);
            e.Title = "New Stats!";
            e.Url = $"https://playoverwatch.com/en-us/career/pc/eu/{Name}";

            EmbedAuthorBuilder author = new EmbedAuthorBuilder();
            author.Name = Name.Split("-")[0];
            author.Url = $"https://playoverwatch.com/en-us/career/pc/eu/{Name}";
            author.IconUrl = stats.avatar;
            e.Author = author;

            EmbedFooterBuilder footer = new EmbedFooterBuilder();
            footer.IconUrl = "http://i.imgur.com/YZ4w2ey.png";
            footer.Text = "Overwatch";
            e.Timestamp = DateTime.Now;
            e.Footer = footer;

            e.ThumbnailUrl = stats.avatar;

            foreach (var kvPair in changedStats)
            {
                e.AddField(kvPair.Key, kvPair.Value, true);
            }

            e.AddField("Sessions most played Hero", $"{mostPlayed.Item2}");
            if (mostPlayed.Item1.Equals("Ana") || mostPlayed.Item1.Equals("Moira") || mostPlayed.Item1.Equals("Orisa") || mostPlayed.Item1.Equals("Doomfist") || 
                mostPlayed.Item1.Equals("Sombra") || mostPlayed.Item1.Equals("Brigitte") || mostPlayed.Item1.Equals("Wrecking-Ball"))
                e.ImageUrl = $"https://blzgdapipro-a.akamaihd.net/hero/{mostPlayed.Item1.ToLower()}/full-portrait.png";
            else
                e.ImageUrl = $"https://blzgdapipro-a.akamaihd.net/media/thumbnail/{mostPlayed.Item1.ToLower()}-gameplay.jpg";

            return e.Build();
        }

        /// <summary>
        /// Checks if there were any changes in major stats
        /// Major stats are: Level, Quickplay Wins, Competitive Wins, Competitive Rank
        /// </summary>
        /// <param Name="oldStats">OStatsResult representing the stats before the Timer elapsed</param>
        /// <param Name="newStats">OStatsResult representing the stats after the Timer elapsed</param>
        /// <returns>A Dictionary with changed stats as Key, and a string presenting them as Value</returns>
        private Dictionary<string, string> getChangedStats(OStatsResult oldStats, OStatsResult newStats)
        {
            Dictionary<string, string> changedStats = new Dictionary<string, string>();

            OverallStats quickNew = newStats.getNotNull().stats.quickplay.overall_stats;
            OverallStats quickOld = oldStats.getNotNull().stats.quickplay.overall_stats;

            if ((quickNew.level + (quickNew.prestige * 100)) > (quickOld.level + (quickOld.prestige * 100)))
            {
                changedStats.Add("Level", (quickNew.level + (quickNew.prestige * 100)).ToString() +
                                $" (+{(quickNew.level + (quickNew.prestige * 100)) - (quickOld.level + (quickOld.prestige * 100))})");
            }

            if (quickNew.wins > quickOld.wins)
            {
                changedStats.Add("Games won", quickNew.wins.ToString() +
                                $" (+{quickNew.wins - quickOld.wins})");
            }

            if (oldStats.getNotNull().stats.competitive != null)
            {
                OverallStats compNew = newStats.getNotNull().stats.competitive.overall_stats;
                OverallStats compOld = oldStats.getNotNull().stats.competitive.overall_stats;

                if (compNew.comprank != compOld.comprank)
                {
                    int difference = compNew.comprank - compOld.comprank;
                    changedStats.Add("Comp Rank", compNew.comprank.ToString() +
                                    $" ({(difference > 0 ? "+" : "") + difference})");
                }

                if (compNew.wins > compOld.wins)
                {
                    changedStats.Add("Comp Games won", compNew.wins.ToString() +
                                    $" (+{compNew.wins - compOld.wins})");
                }
            }

            if(quickNew.endorsement_level != quickOld.endorsement_level){
                changedStats.Add("Endorsement Level", quickNew.endorsement_level.ToString());
            }

            return changedStats;
        }

        /// <summary>
        /// Determines the most played Hero of the last play session
        /// </summary>
        /// <param Name="oldStats">Playtime of each hero before the Timer elapsed</param>
        /// <param Name="newStats">Playtime of each hero after the Timer elapsed</param>
        /// <returns>A Tuple with the Name of the most played Hero, and a string presenting the change in playtime</returns>
        private Tuple<string, string> getSessionMostPlayed(Playtime oldStats, Playtime newStats)
        {
            var New = newStats.merge();
            var Old = oldStats.merge();
            var difference = new Dictionary<string, double>();

            foreach (string key in Old.Keys)
                difference.Add(key, New[key] - Old[key]);

            var sortedList = (from entry in difference orderby entry.Value descending select entry).ToList();
            string leaderboard = "";
            for (int i = 0; i < 5; i++)
            {
                if (sortedList[i].Value > 0.005)
                    leaderboard += $"{sortedList[i].Key}: {Math.Round(New[sortedList[i].Key], 2)}hrs (+{Math.Round(sortedList[i].Value, 2)})\n";
                else
                    break;
            }

            if (difference[sortedList[0].Key] > 0.005)
                return Tuple.Create(sortedList[0].Key, leaderboard);

            return Tuple.Create("CannotFetchArcade", "CannotFetchArcade");
        }

        private static Tuple<string, string> getMostPlayed(Playtime stats)
        {
            var playtime = stats.merge();
            var sortedList = (from entry in playtime orderby entry.Value descending select entry).ToList();
            string leaderboard = "";
            for (int i = 0; i < 5; i++)
            {
                if (sortedList[i].Value > 0.005)
                    leaderboard += $"{sortedList[i].Key}: {Math.Round(playtime[sortedList[i].Key], 2)}hrs\n";
                else
                    break;
            }

            if (playtime[sortedList[0].Key] > 0.005)
                return Tuple.Create(sortedList[0].Key, leaderboard);

            return Tuple.Create("CannotFetchArcade", "CannotFetchArcade");
        }

        public override string TrackerUrl(){
            return "https://playoverwatch.com/en-us/career/pc/eu/" + Name;
        }
    }
}