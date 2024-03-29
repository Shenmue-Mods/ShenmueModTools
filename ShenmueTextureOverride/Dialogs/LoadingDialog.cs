﻿using ShenmueDKSharp.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShenmueHDArchiver.Dialogs
{
    public partial class LoadingDialog : Form
    {
        private IProgressable m_progressable;
        Thread m_thread;

        public LoadingDialog()
        {
            InitializeComponent();
        }

        private const int CP_NOCLOSE_BUTTON = 0x200;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams myCp = base.CreateParams;
                myCp.ClassStyle = myCp.ClassStyle | CP_NOCLOSE_BUTTON;
                return myCp;
            }
        }

        public DialogResult ShowDialog(Thread thread)
        {
            m_thread = thread;
            return ShowDialog();
        }

        public void SetProgessable(IProgressable progressable)
        {
            if (progressable.IsAbortable)
            {
                MinimumSize = new Size(350, 125);
                MaximumSize = new Size(350, 125);
                button_Abort.Visible = true;
            }
            else
            {
                MinimumSize = new Size(350, 100);
                MaximumSize = new Size(350, 100);
                button_Abort.Visible = false;
            }
            m_progressable = progressable;
            progressable.Finished += Progressable_Finished;
            progressable.DescriptionChanged += Progressable_DescriptionChanged;
            progressable.ProgressChanged += Progressable_ProgressChanged;
            progressable.Error += Progressable_Error;
        }

        private void LoadingDialog_Shown(object sender, EventArgs e)
        {
            m_thread.Start();
        }

        private void Progressable_Error(object sender, ErrorArgs e)
        {
            MessageBox.Show(e.Error, "Error occured!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Progressable_Finished(object sender, FinishedArgs e)
        {
            if (Visible)
                Invoke((MethodInvoker)delegate {
                    if (!e.Successful)
                    {
                        Console.WriteLine("FAILED: {0}", sender.GetType());
                    }
                    Close();
                });
        }

        private void Progressable_ProgressChanged(object sender, ProgressChangedArgs e)
        {
            if (Visible)
                Invoke((MethodInvoker)delegate {
                    if (progressBar_Progress.Value == e.Progress) return;
                    if (e.Progress < 100) progressBar_Progress.Value = e.Progress + 1;
                    progressBar_Progress.Value = e.Progress;
                });
        }

        private void Progressable_DescriptionChanged(object sender, DescriptionChangedArgs e)
        {
            if (Visible)
                Invoke((MethodInvoker)delegate {
                    label_Description.Text = e.Description;
                });
        }

        private void button_Abort_Click(object sender, EventArgs e)
        {
            m_progressable.Abort();
        }
    }
}
