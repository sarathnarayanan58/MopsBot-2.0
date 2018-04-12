﻿using System;
using Discord;
using Discord.WebSocket;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OxyPlot;

namespace MopsBot.Data.Session
{
    public class Poll
    {
        public string question;
        public string[] answers;
        public int[] results;
        public List<IGuildUser> participants;
        private PlotModel viewerChart;
        private OxyPlot.Series.PieSeries series;
        public string ID;

        private void initPlot()
        {
            viewerChart = new PlotModel();
            viewerChart.Title = question;
            viewerChart.TextColor = OxyColor.FromRgb(175, 175, 175);
            viewerChart.LegendFontSize = 24;
            viewerChart.LegendPosition = LegendPosition.BottomCenter;

            series = new OxyPlot.Series.PieSeries();
            viewerChart.Series.Add(series);
        }

        /// <summary>
        /// Saves the plot as a .png and returns the URL.
        /// </summary>
        /// <returns>The URL</returns>
        public string DrawPlot()
        {
            using (var stream = File.Create($"mopsdata//{ID}plot.pdf"))
            {
                var pdfExporter = new PdfExporter { Width = 800, Height = 400 };
                pdfExporter.Export(viewerChart, stream);
            }

            var prc = new System.Diagnostics.Process();
            prc.StartInfo.FileName = "convert";
            prc.StartInfo.Arguments = $"-set density 300 \"mopsdata//{ID}plot.pdf\" \"//var//www//html//StreamCharts//{ID}plot.png\"";

            prc.Start();

            prc.WaitForExit();

            var dir = new DirectoryInfo("mopsdata//");
            var files = dir.GetFiles().Where(x => x.Extension.ToLower().Equals($"{ID}.pdf"));
            foreach (var f in files)
                f.Delete();

            return $"http://5.45.104.29/StreamCharts/{ID}plot.png?rand={StaticBase.ran.Next(0,999999999)}";
        }

        /// <summary>
        /// Adds a Value to the plot, to its' current Title
        /// </summary>
        /// <param name="value">The Value to add to the plot</param>
        public void AddValue(string name, double value)
        {
            if(series.Slices.Where(x => x.Label.Equals(name)).Count() == 0)
                series.Slices.Add(new OxyPlot.Series.PieSlice(name, value));
            else{
                var toRemove = series.Slices.First(x => x.Label.Equals(name));
                var toAdd = new OxyPlot.Series.PieSlice(name, toRemove.Value + value);
                series.Slices.Remove(toRemove);
                series.Slices.Add(toAdd);
            }
        }

        public Poll(string q, string[] a, IGuildUser[] p)
        {
            initPlot();
            question = q;
            answers = a;
            ID = question.Replace(" ", "_");
            foreach(string answer in answers){
                AddValue(answer, 0);
            }
            participants = p.ToList();
        }
    }
}
