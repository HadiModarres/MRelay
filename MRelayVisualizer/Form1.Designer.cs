

using System.Net;
using System.Threading;
using ZedGraph;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Windows;

namespace MRelayVisualizer
{
    partial class Form1
    {
        Relay.Relay testRelay;
        System.Windows.Forms.Timer timer;
        EncryptedRelay.EncryptedRelay eRelay;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            
            
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        /// 
        private void initR()
        {
            Console.WriteLine("tr");
            testRelay = new Relay.Relay(8000, 1, Dns.GetHostAddresses("149.210.142.87")[0], 4501, 1, 1024, 1024, 1024, 1024 * 64, true, true);
            
        }
        private void initE()
        {
           
            eRelay = new EncryptedRelay.EncryptedRelay(4000,Dns.GetHostAddresses("127.0.0.1")[0],8000,true);
            Console.WriteLine("er");
        }
        
           
        private void InitializeComponent()

        {

           
            this.components = new System.ComponentModel.Container();
            this.zedGraphControl1 = new ZedGraph.ZedGraphControl();
            this.SuspendLayout();
            // 
            // zedGraphControl1
            // 
            this.zedGraphControl1.Location = new System.Drawing.Point(-2, -3);
            this.zedGraphControl1.Name = "zedGraphControl1";
            this.zedGraphControl1.ScrollGrace = 0D;
            this.zedGraphControl1.ScrollMaxX = 0D;
            this.zedGraphControl1.ScrollMaxY = 0D;
            this.zedGraphControl1.ScrollMaxY2 = 0D;
            this.zedGraphControl1.ScrollMinX = 0D;
            this.zedGraphControl1.ScrollMinY = 0D;
            this.zedGraphControl1.ScrollMinY2 = 0D;
            this.zedGraphControl1.Size = new System.Drawing.Size(688, 397);
            this.zedGraphControl1.TabIndex = 0;
            
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(682, 393);
            this.Controls.Add(this.zedGraphControl1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }
        private void updateGraph(Object timerObject,EventArgs eventArgs)
        {
            if ((testRelay== null)) {

                return;
            }
            if (testRelay.Monitor == null)
                return;
            Console.WriteLine("updating");
            ZedGraphControl zgc = zedGraphControl1;
            List<RelayMonitor.MonitorObject> l1 = testRelay.Monitor.GetSpeedValues();
            GraphPane myPane = zgc.GraphPane;
            myPane.CurveList.Clear();
            for (int i=0;i<l1.Count;i++)
            {
                PointPairList list = getListForMonitorObject(l1[i]);
                myPane.AddCurve("",
                  list, Color.Black, SymbolType.Diamond);
            }

            zgc.AxisChange();
            this.Refresh();
            
        }
        private PointPairList getListForMonitorObject(RelayMonitor.MonitorObject mo)
        {
            PointPairList list = new PointPairList();
            for (int i=0;i<mo.SpeedHistory.Length;i++)
            {
                list.Add(i+mo.Start,mo.SpeedHistory[i]);
            }
            return list;
        }
        private void CreateGraph(ZedGraphControl zgc)
        {
            // get a reference to the GraphPane
            GraphPane myPane = zgc.GraphPane;

            // Set the Titles
            myPane.Title.Text = "My Test Graph\n(For CodeProject Sample)";
            myPane.XAxis.Title.Text = "My X Axis";
            myPane.YAxis.Title.Text = "My Y Axis";
            

            // Make up some data arrays based on the Sine function
            double x, y1, y2;
            PointPairList list1 = new PointPairList();
            PointPairList list2 = new PointPairList();
            for (int i = 0; i < 36; i++)
            {
                x = (double)i + 5;
                y1 = 1.5 + Math.Sin((double)i * 0.2);
                y2 = 3.0 * (1.5 + Math.Sin((double)i * 0.2));
                list1.Add(x, y1);
                list2.Add(x, y2);
            }

            // Generate a red curve with diamond
            // symbols, and "Porsche" in the legend
            LineItem myCurve = myPane.AddCurve("Porsche",
                  list1, Color.Red, SymbolType.Diamond);

            // Generate a blue curve with circle
            // symbols, and "Piper" in the legend
            LineItem myCurve2 = myPane.AddCurve("Piper",
                  list2, Color.Blue, SymbolType.Circle);
            
            // Tell ZedGraph to refigure the
            // axes since the data have changed
            zgc.AxisChange();
        }

        #endregion

        private ZedGraph.ZedGraphControl zedGraphControl1;
    }
}

