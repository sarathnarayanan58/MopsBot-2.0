using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Newtonsoft.Json;
using MopsBot.Data.Tracker.APIResults.TwitchClip;
using System.Threading.Tasks;
using System.Xml;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Attributes;

namespace MopsBot.Data.Tracker
{
    public class TwitchClipTracker : ITracker
    {
        public uint ViewThreshold;
        
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<DateTime, KeyValuePair<int, double>> TrackedClips;
        public TwitchClipTracker() : base(600000, ExistingTrackers * 2000)
        {
        }

        public TwitchClipTracker(string streamerName) : base(600000)
        {
            Console.WriteLine("\n" + $"{DateTime.Now} Started TwitchClipTracker for {streamerName}");
            Name = streamerName;
            TrackedClips = new Dictionary<DateTime, KeyValuePair<int, double>>();
            ChannelMessages = new Dictionary<ulong, string>();
            ViewThreshold = 2;

            try
            {
                string query = MopsBot.Module.Information.ReadURLAsync($"https://api.twitch.tv/kraken/channels/{Name}?client_id={Program.Config["Twitch"]}").Result;
                APIResults.Twitch.Channel checkExists = JsonConvert.DeserializeObject<APIResults.Twitch.Channel>(query);
                var test = checkExists.broadcaster_language;
            }
            catch (Exception)
            {
                Dispose();
                throw new Exception($"Streamer {TrackerUrl()} could not be found on Twitch!");
            }
        }

        protected async override void CheckForChange_Elapsed(object stateinfo)
        {
            try
            {
                TwitchClipResult clips = await getClips();
                foreach (var datetime in TrackedClips.Keys.ToList())
                {
                    if (datetime.AddMinutes(30) <= DateTime.UtcNow){
                        TrackedClips.Remove(datetime);
                        await StaticBase.Trackers[TrackerType.TwitchClip].UpdateDBAsync(this);
                    }
                }

                foreach (Clip clip in clips.clips)
                {
                    var embed = createEmbed(clip);
                    foreach (ulong channel in ChannelMessages.Keys)
                    {
                        await OnMajorChangeTracked(channel, embed, ChannelMessages[channel]);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" +  $"[Error] by {Name} at {DateTime.Now}:\n{e.Message}\n{e.StackTrace}");
            }
        }

        private async Task<TwitchClipResult> getClips()
        {
            return await NextPage(Name);
        }

        private async Task<TwitchClipResult> NextPage(string name, TwitchClipResult clips = null, string cursor = "")
        {
            if (clips == null)
            {
                clips = new TwitchClipResult();
                clips.clips = new List<Clip>();
            }
            try
            {
                var acceptHeader = new KeyValuePair<string, string>("Accept", "application/vnd.twitchtv.v5+json");
                string query = await MopsBot.Module.Information.ReadURLAsync($"https://api.twitch.tv/kraken/clips/top?client_id={Program.Config["Twitch"]}&channel={name}&period=day{(!cursor.Equals("") ? $"&cursor={cursor}" : "")}", acceptHeader);

                JsonSerializerSettings _jsonWriter = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };

                var tmpResult = JsonConvert.DeserializeObject<TwitchClipResult>(query, _jsonWriter);
                if (tmpResult.clips != null)
                {
                    foreach (var clip in tmpResult.clips.Where(p => !TrackedClips.ContainsKey(p.created_at) && p.created_at > DateTime.UtcNow.AddMinutes(-30) && p.views >= ViewThreshold))
                    {
                        if(clip.vod != null && !TrackedClips.Any(x => {
                                double matchingDuration = 0;

                                if(clip.vod.offset < x.Value.Key)
                                    matchingDuration = (clip.vod.offset + clip.duration > x.Value.Key + x.Value.Value) ? x.Value.Value : clip.vod.offset + clip.duration - x.Value.Key;
                                else
                                    matchingDuration = (x.Value.Key + x.Value.Value > clip.vod.offset + clip.duration) ? clip.duration : x.Value.Key + x.Value.Value - clip.vod.offset;

                                double matchingPercentage = matchingDuration / clip.duration;
                                return matchingPercentage > 0.2;
                            })){

                            TrackedClips.Add(clip.created_at, new KeyValuePair<int, double>(clip.vod.offset, clip.duration));
                            clips.clips.Add(clip);
                        } else if (clip.vod == null){
                            TrackedClips.Add(clip.created_at, new KeyValuePair<int, double>(-60, clip.duration));
                            clips.clips.Add(clip);
                        }
                        
                        await StaticBase.Trackers[TrackerType.TwitchClip].UpdateDBAsync(this);
                    }
                    if (!tmpResult._cursor.Equals(""))
                    {
                        return await NextPage(name, clips, tmpResult._cursor);
                    }
                }
                return clips;
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" +  $"[ERROR] by TwitchClipTracker for {Name} at {DateTime.Now}:\n{e.Message}\n{e.StackTrace}");
                return new TwitchClipResult();
            }
        }

        private Embed createEmbed(Clip clip)
        {
            EmbedBuilder e = new EmbedBuilder();
            e.Color = new Color(0x6441A4);
            e.Title = clip.title;
            e.Url = clip.url;
            e.Timestamp = clip.created_at;

            EmbedAuthorBuilder author = new EmbedAuthorBuilder();
            author.Name = Name;
            author.Url = clip.broadcaster.channel_url;
            author.IconUrl = clip.broadcaster.logo;
            e.Author = author;

            EmbedFooterBuilder footer = new EmbedFooterBuilder();
            footer.IconUrl = "https://media-elerium.cursecdn.com/attachments/214/576/twitch.png";
            footer.Text = "Twitch";
            e.Footer = footer;

            e.ImageUrl = clip.thumbnails.medium;

            e.AddField("Length", clip.duration + " seconds", true);
            e.AddField("Views", clip.views, true);
            e.AddField("Game", (clip.game == null || clip.game.Equals("")) ? "Nothing" : clip.game, true);
            e.AddField("Creator", $"[{clip.curator.name}]({clip.curator.channel_url})", true);

            return e.Build();
        }

        public override string TrackerUrl(){
            return $"https://www.twitch.tv/{Name}/clips";
        }
    }
}