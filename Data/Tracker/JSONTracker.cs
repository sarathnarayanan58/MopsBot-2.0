using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using NewsAPI;
using NewsAPI.Constants;
using NewsAPI.Models;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using MopsBot.Data.Entities;

namespace MopsBot.Data.Tracker
{
    [MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElements]
    public class JSONTracker : BaseTracker
    {
        [MongoDB.Bson.Serialization.Attributes.BsonDictionaryOptions(MongoDB.Bson.Serialization.Options.DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, string> PastInformation;
        public List<string> ToTrack;
        public DatePlot DataGraph;

        public JSONTracker() : base()
        {
        }

        public JSONTracker(Dictionary<string, string> args) : base()
        {
            base.SetBaseValues(args);
            ToTrack = args["Locations"].Split(null).ToList();
            Name = args["_Name"] + "|||" + String.Join(",", ToTrack);

            //Check if Name ist valid
            try
            {
                var test = new JSONTracker(Name);
                PastInformation = test.PastInformation;
                test.Dispose();
                SetTimer();
            }
            catch (Exception e)
            {
                this.Dispose();
                throw e;
            }

            if (StaticBase.Trackers[TrackerType.JSON].GetTrackers().ContainsKey(Name))
            {
                this.Dispose();

                args["Id"] = Name;
                var curTracker = StaticBase.Trackers[TrackerType.JSON].GetTrackers()[Name];
                curTracker.ChannelMessages[ulong.Parse(args["Channel"].Split(":")[1])] = args["Notification"];
                StaticBase.Trackers[TrackerType.JSON].UpdateContent(new Dictionary<string, Dictionary<string, string>> { { "NewValue", args }, { "OldValue", args } }).Wait();

                throw new ArgumentException($"Tracker for {args["_Name"]} existed already, updated instead!");
            }
        }

        public JSONTracker(string name) : base()
        {
            //name = name.Replace(" ", string.Empty);
            Name = name;
            ToTrack = name.Split("|||")[1].Replace(" ", string.Empty).Split(",").ToList();

            //Check if name yields proper results.
            try
            {
                PastInformation = getResults().Result;
                var graphMembers = PastInformation.Where(x => x.Key.Contains("graph:"));
                foreach(var graphTest in graphMembers){
                    if(!graphTest.Equals(default(KeyValuePair<string,string>))){
                        bool succeeded = double.TryParse(graphTest.Value, out double test);

                        if(succeeded){
                            if(DataGraph == null) DataGraph = new DatePlot("JSON" + Name.GetHashCode(), "Date", "Value", format: "dd-MMM", relativeTime: false, multipleLines: true);
                            DataGraph.AddValueSeperate(graphTest.Key, test, relative:false);
                        }

                        else throw new Exception("Graph value is not a number!");
                    }
                }
                SetTimer();
            }
            catch (Exception)
            {
                Dispose();
                throw new Exception($"{Name} could not be resolved, using the given paths.");
            }
        }

        public async override void PostInitialisation()
        {
            if (DataGraph != null)
                DataGraph.InitPlot("Date", "Value", format: "dd-MMM", relative: false);
        }

        protected async override void CheckForChange_Elapsed(object stateinfo)
        {
            try
            {
                var newInformation = await getResults();

                if(PastInformation == null) PastInformation = newInformation;

                var embed = createEmbed(newInformation, PastInformation, out bool changed);
                if(changed){
                    var graphMembers = newInformation.Where(x => x.Key.Contains("graph:"));

                    foreach(var graphValue in graphMembers){
                        if(!graphValue.Equals(default(KeyValuePair<string,string>))){
                            DataGraph.AddValueSeperate(graphValue.Key, double.Parse(PastInformation[graphValue.Key]), relative: false);
                            DataGraph.AddValueSeperate(graphValue.Key, double.Parse(graphValue.Value), relative: false);
                        }
                    }

                    foreach (var channel in ChannelMessages.Keys.ToList()){
                        await OnMajorChangeTracked(channel, DataGraph == null ? embed : createEmbed(newInformation, PastInformation, out changed), ChannelMessages[channel]);
                    }

                    PastInformation = newInformation;
                    await StaticBase.Trackers[TrackerType.JSON].UpdateDBAsync(this);
                }
            }
            catch (Exception e)
            {
                await Program.MopsLog(new LogMessage(LogSeverity.Error, "", $" error by {Name}", e));
            }
        }

        private async Task<Dictionary<string, string>> getResults()
        {
            var json = await FetchJSONDataAsync<dynamic>(TrackerUrl());
            var result = new Dictionary<string, string>();

            foreach(string cur in ToTrack){
                string curMod = cur;
                if(curMod.Contains("graph:")) curMod = curMod.Replace("graph:", string.Empty);
                if(curMod.Contains("always:")) curMod = curMod.Replace("always:", string.Empty);
                string[] keywords = curMod.Split("->");
                var tmpJson = json;

                foreach(string keyword in keywords){
                    int.TryParse(keyword, out int index);
                    if(index > 0) tmpJson = tmpJson[index - 1];
                    else tmpJson = tmpJson[keyword];
                }

                result[cur] = tmpJson.ToString();
            }

            return result;
        }

        private Embed createEmbed(Dictionary<string, string> newInformation, Dictionary<string, string> oldInformation, out bool changed)
        {
            var embed = new EmbedBuilder();
            embed.WithColor(255, 227, 21);
            embed.WithTitle("Change tracked").WithUrl(TrackerUrl()).WithCurrentTimestamp();
            embed.WithFooter(x => {
                                   x.Text = "JsonTracker"; 
                                   x.IconUrl="https://upload.wikimedia.org/wikipedia/commons/thumb/c/c9/JSON_vector_logo.svg/160px-JSON_vector_logo.svg.png";
                            });

            changed = false;
            foreach(var kvp in newInformation){
                string oldS = oldInformation[kvp.Key];
                string newS = kvp.Value;

                if(!newS.Equals(oldS)){
                    changed = true;
                    embed.AddField(kvp.Key.Split("->").Last(), $"{oldS} -> {newS}", true);
                }
                else if(kvp.Key.Contains("always:")){
                    embed.AddField(kvp.Key.Split("->").Last(), newS, true);
                }
            }

            if(DataGraph != null) embed.ImageUrl = DataGraph.DrawPlot();

            return embed.Build();
        }

        public override string TrackerUrl()
        {
            return Name.Split("|||")[0];
        }

        public override Dictionary<string, object> GetParameters(ulong guildId){
            var parameters = base.GetParameters(guildId);
            (parameters["Parameters"] as Dictionary<string, object>)["Locations"] = "";
            return parameters;
        }

        public override void Update(Dictionary<string, Dictionary<string, string>> args){
            base.Update(args);
            ToTrack = args["NewValue"]["Locations"].Split(null).ToList();
            Name = args["NewValue"]["_Name"] + String.Join(",", ToTrack);
        }

        public override object GetAsScope(ulong channelId){
            return new ContentScope(){
                Id = this.Name,
                _Name = this.Name.Split("|||")[0],
                Locations = String.Join("\n", this.ToTrack),
                Notification = this.ChannelMessages[channelId],
                Channel = "#" + ((SocketGuildChannel)Program.Client.GetChannel(channelId)).Name + ":" + channelId
            };
        }

        public new struct ContentScope
        {
            public string Id;
            public string _Name;
            public string Locations;
            public string Notification;
            public string Channel;
        }
    }
}