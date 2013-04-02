﻿/*
 * Copyright (c) 2012-2013, Jaap de Haan <jaap.dehaan@color-of-code.de>
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
using System.ComponentModel;
using Funani.Api;

namespace Funani.Gui.Controls.Progress
{
    public class CommandProgressViewModel : INotifyPropertyChanged
    {
        private readonly ICommandQueue _model;
        private String _eta;
        private String _info;
        private Int32 _performed;
        private Int32 _total;

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public CommandProgressViewModel(ICommandQueue model)
        {
            _model = model;
            BindEvents();
        }

        public Int32 Total
        {
            get { return _total; }
            set
            {
                _total = value;
                OnPropertyChanged("Total");
            }
        }

        public Int32 Performed
        {
            get { return _performed; }
            set
            {
                _performed = value;
                OnPropertyChanged("Performed");
            }
        }

        public String Info
        {
            get { return _info; }
            set
            {
                _info = value;
                OnPropertyChanged("Info");
            }
        }

        public String Eta
        {
            get { return _eta; }
            set
            {
                _eta = value;
                OnPropertyChanged("Eta");
            }
        }

        private void model_CommandStarted(object sender, CommandProgressEventArgs e)
        {
            Info = e.Command.ToString();
        }

        private void model_CommandEnded(object sender, CommandProgressEventArgs e)
        {
            Performed++;
            Total = _model.Count;
            //TODO: refresh Eta
        }

        private void model_ThreadStarted(object sender, EventArgs e)
        {
            Performed = 0;
            Total = _model.Count;
            Info = String.Empty;
            Eta = String.Empty;
        }

        private void model_ThreadEnded(object sender, EventArgs e)
        {
            Performed = Total;
            Info = String.Format("{0} actions performed", Total);
        }

        private void BindEvents()
        {
            _model.ThreadStarted += model_ThreadStarted;
            _model.ThreadEnded += model_ThreadEnded;
            _model.CommandStarted += model_CommandStarted;
            _model.CommandEnded += model_CommandEnded;
        }
    }
}