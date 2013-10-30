﻿/*
 * Copyright (c) 2008-2013, Jaap de Haan <jaap.dehaan@color-of-code.de>
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 
 *   * Redistributions of source code must retain the above copyright notice,
 *     this list of conditions and the following disclaimer.
 *   * Redistributions in binary form must reproduce the above copyright
 *     notice, this list of conditions and the following disclaimer in the
 *     documentation and/or other materials provided with the distribution.
 *   * Neither the name of the "Color-Of-Code" nor the names of its
 *     contributors may be used to endorse or promote products derived from
 *     this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
 * THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Catel.Data;
using Catel.IoC;
using Catel.MVVM;
using Funani.Api;
using Funani.Api.Metadata;
using MongoDB.Bson;
using MongoDB.Driver;
using OxyPlot;
using OxyPlot.Series;

namespace Funani.Gui.ViewModels
{
    public class MongoDbViewModel : ViewModelBase, IConsoleRedirect
    {
        private readonly Dispatcher _dispatcher;
        private readonly IEngine _engine;

        public MongoDbViewModel(Dispatcher dispatcher)
        {
            _engine = DependencyResolver.Resolve<IEngine>();
            _dispatcher = dispatcher;

            Lines = new ObservableCollection<string>();
            
            // commands
            GetStatistics = new Command(OnGetStatisticsExecute);
            Backup = new Command(OnBackupExecute);
            RunQuery = new Command(OnRunQueryExecute);
        }

        private MongoDatabase Funani
        {
            get { return _engine.MetadataDatabase as MongoDatabase; }
        }

        public void OnOutputDataReceived(string data)
        {
            if (data != null)
            {
                _dispatcher.BeginInvoke((Action)(() =>
                                                 Lines.Add(data.TrimEnd())));
            }
        }

        public void OnErrorDataReceived(string data)
        {
            if (data != null)
            {
                _dispatcher.BeginInvoke((Action)(() =>
                                                 Lines.Add(data.TrimEnd())));
            }
        }

        #region Commands

        #region Command: GetStatistics

        /// <summary>
        ///     Gets the GetStatistics command.
        /// </summary>
        public Command GetStatistics { get; private set; }

        /// <summary>
        ///     Method to invoke when the GetStatistics command is executed.
        /// </summary>
        private void OnGetStatisticsExecute()
        {
            Statistics = Funani.GetStats();
            LoadData();
        }

        #endregion

        #region Command: Backup

        /// <summary>
        ///     Gets the Backup command.
        /// </summary>
        public Command Backup { get; private set; }

        /// <summary>
        ///     Method to invoke when the Backup command is executed.
        /// </summary>
        private void OnBackupExecute()
        {
            _engine.Backup();
        }

        #endregion

        #region Command: RunQuery

        /// <summary>
        ///     Gets the RunQuery command.
        /// </summary>
        public Command RunQuery { get; private set; }

        /// <summary>
        ///     Method to invoke when the RunQuery command is executed.
        /// </summary>
        private void OnRunQueryExecute()
        {
            if (CollectionNames == null)
                CollectionNames = Funani.GetCollectionNames();
            try
            {
                QueryException = null; // reset
                var bsonJS = new BsonJavaScript(Query);
                BsonValue result = Funani.Eval(bsonJS);
                QueryResults = result.ToJson().Split();
            }
            catch (Exception ex)
            {
                QueryException = ex;
            }
        }

        #endregion

        #endregion

        private void LoadData()
        {
            var groupByMimeTypeOperation = new BsonDocument
                {
                    {
                        "$group",
                        new BsonDocument
                            {
                                { "_id", "$MimeType" },
                                {
                                    "SumCount",
                                    new BsonDocument { { "$sum", 1 } }
                                },
                                {
                                    "SumSize",
                                    new BsonDocument { { "$sum", "$FileSize" } }
                                }
                            }
                    }
                };

            var sortOperation = new BsonDocument { { "$sort", new BsonDocument { { "SumSize", -1 } } } };

            var db = _engine.MetadataDatabase as MongoDatabase;
            AggregateResult result = db.GetCollection<FileInformation>("fileinfo").Aggregate(
                groupByMimeTypeOperation,
                sortOperation);

            if (result.Ok)
            {
                var seriesCount = new PieSeries();
                var seriesSize = new PieSeries();

                var list = result.ResultDocuments
                                 .Select(doc => new
                                     {
                                         Mime = MimeMap(doc["_id"].AsString),
                                         Size = doc["SumSize"].AsInt64 / 1024.0 / 1024.0 / 1024.0,
                                         Count = doc["SumCount"].AsInt32
                                     });

                foreach (var doc in list.GroupBy(x => x.Mime)
                                        .Select(doc => new
                                            {
                                                Mime = doc.Key,
                                                Size = doc.Sum(x => x.Size),
                                                Count = doc.Sum(x => x.Count)
                                            })
                                        .OrderByDescending(x => x.Size))
                {
                    double size = doc.Size;
                    int count = doc.Count;
                    seriesSize.Slices.Add(
                        new PieSlice
                            {
                                Label = String.Format("{0:0.0} GB: {1}", size, doc.Mime),
                                Value = size
                            });
                    seriesCount.Slices.Add(
                        new PieSlice
                            {
                                Label = String.Format("{0}: {1}", count, doc.Mime),
                                Value = count
                            });
                }

                seriesCount.InnerDiameter = 0.2;
                seriesCount.ExplodedDistance = 0;
                seriesCount.Stroke = OxyColors.Black;
                seriesCount.StrokeThickness = 1.0;
                seriesCount.AngleSpan = 360;
                seriesCount.StartAngle = 0;

                seriesSize.InnerDiameter = 0.2;
                seriesSize.ExplodedDistance = 0;
                seriesSize.Stroke = OxyColors.Black;
                seriesSize.StrokeThickness = 1.0;
                seriesSize.AngleSpan = 360;
                seriesSize.StartAngle = 0;

                var modelCount = new PlotModel("Count by MIME type");
                var modelSize = new PlotModel("Size by MIME type");

                modelCount.Series.Add(seriesCount);
                modelSize.Series.Add(seriesSize);

                MimeSizePlotModel = modelSize;
                MimeCountPlotModel = modelCount;
            }
        }

        private String MimeMap(String input)
        {
            if (input.StartsWith("image/"))
                return "images";
            if (input.StartsWith("video/"))
                return "videos";
            if (input == "application/pdf")
                return "documents";
            if (input == "application/msword")
                return "documents";
            return "others";
        }

        #region Properties

        public ObservableCollection<String> Lines { get; set; }

        public String Query { get; set; }

        public IList<String> QueryResults { get; set; }

        public Exception QueryException { get; set; }

        public IEnumerable<String> CollectionNames { get; set; }

        public PlotModel MimeCountPlotModel { get; set; }

        public PlotModel MimeSizePlotModel { get; set; }

        public DatabaseStatsResult Statistics { get; set; }

        #endregion
    }
}