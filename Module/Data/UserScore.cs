﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Threading.Tasks;

namespace MopsBot.Module.Data
{
    class UserScore
    {
        public Dictionary<ulong, Individual.User> users = new Dictionary<ulong, Individual.User>();

        public UserScore()
        {
            StreamReader read = new StreamReader(new FileStream("data//scores.txt", FileMode.OpenOrCreate));

            string fs = "";
            while ((fs = read.ReadLine()) != null)
            {
                string[] s = fs.Split(':');
                Individual.User user = new Individual.User(int.Parse(s[1]), int.Parse(s[2]), int.Parse(s[3]), int.Parse(s[4]), int.Parse(s[5]));
                users.Add(ulong.Parse(s[0]), user);
            }
            read.Dispose();
        }

        public void writeScore()
        {
            StreamWriter write = new StreamWriter(new FileStream("data//scores.txt", FileMode.Create));
            write.AutoFlush = true;
            foreach (var that in users)
            {
                var user = that.Value;
                write.WriteLine($"{that.Key}:{user.Score}:{user.Experience}:{user.punched}:{user.hugged}:{user.kissed}");
            }

            write.Dispose();
        }

        public void addStat(ulong id, int value, string stat)
        {
            if (!users.ContainsKey(id))
            {
                users.Add(id, new Individual.User(0, 0, 0, 0, 0));
            }

            switch (stat.ToLower())
            {
                case "experience":
                    users[id].Experience += value;
                    break;
                case "score":
                    users[id].Score += value;
                    break;
                case "hug":
                    users[id].hugged += value;
                    break;
                case "kiss":
                    users[id].kissed += value;
                    break;
                case "punch":
                    users[id].punched += value;
                    break;
                default:
                    return;
            }
            writeScore();
        }

        public string drawDiagram(int count)
        {
            var sortedDict = (from entry in users orderby entry.Value.Experience descending select entry).Take(count).ToArray();

            int maximum = 0;
            string[] lines = new string[count];

            maximum = sortedDict[0].Value.calcLevel();

            for (int i = 0; i < count; i++)
            {
                Individual.User user = sortedDict[i].Value;
                lines[i] = (i + 1).ToString().Length < 2 ? $"#{i + 1} |" : $"#{i + 1}|";
                double relPercent = user.calcLevel() / ((double)maximum / 10);
                for (int j = 0; j < relPercent; j++)
                {
                    lines[i] += "■";
                }
                lines[i] += $"  ({user.calcLevel()} / {(Program.client.GetUser(sortedDict[i].Key) == null ? "" + sortedDict[i].Key : Program.client.GetUser(sortedDict[i].Key).Username)})";
            }


            string output = "```" + string.Join("\n", lines) + "```";

            return output;
        }
    }
}