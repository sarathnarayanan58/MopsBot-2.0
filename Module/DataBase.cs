﻿using System;
using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MopsBot.Data.Entities;

namespace MopsBot.Module
{
    public class DataBase : ModuleBase
    {
        [Command("Hug", RunMode = RunMode.Async)]
        [Summary("Hugs the specified person")]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task hug(SocketGuildUser person)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (!person.Id.Equals(Context.User.Id))
                {
                    await User.ModifyUserAsync(person.Id, x => x.Hugged++);
                    await ReplyAsync($"Aww, **{person.Username}** got hugged by **{Context.User.Username}**.\n" +
                                     $"They have already been hugged {(await User.GetUserAsync(person.Id)).Hugged} times!");
                }
                else
                    await ReplyAsync("Go ahead.");
            }
        }

        [Command("Kiss", RunMode = RunMode.Async)]
        [Summary("Smooches the specified person")]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task kiss(SocketGuildUser person)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (!person.Id.Equals(Context.User.Id))
                {
                    await User.ModifyUserAsync(person.Id, x => x.Kissed++);
                    await ReplyAsync($"Mwaaah, **{person.Username}** got kissed by **{Context.User.Username}**.\n" +
                                     $"They have already been kissed {(await User.GetUserAsync(person.Id)).Kissed} times!");
                }
                else
                    await ReplyAsync("That's sad.");
            }
        }

        [Command("Punch", RunMode = RunMode.Async)]
        [Summary("Fucks the specified person up")]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task punch(SocketGuildUser person)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (!person.Id.Equals(Context.User.Id))
                {
                    await User.ModifyUserAsync(person.Id, x => x.Punched++);
                    await ReplyAsync($"DAAMN! **{person.Username}** just got fucked up by **{Context.User.Username}**.\n" +
                                     $"That's {(await User.GetUserAsync(person.Id)).Punched} times, they have been fucked up now.");
                }
                else
                    await ReplyAsync("Please don't fuck yourself up. That's unhealthy.");
            }
        }


        [Command("GetStats", RunMode = RunMode.Async)]
        [Summary("Returns your or another persons experience and all that stuff")]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task GetStats(SocketGuildUser user = null)
        {
            using (Context.Channel.EnterTypingState())
            {
                await ReplyAsync("", embed: (await User.GetUserAsync(user?.Id ?? Context.User.Id)).StatEmbed());
            }
        }

        /*[Command("ranking")]
        [Summary("Returns the top 10 list of level")]
        [RequireBotPermission(ChannelPermission.SendMessages)]
        public async Task ranking(int limit, string stat = "level")
        {
            
        }*/
    }
}