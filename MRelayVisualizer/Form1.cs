using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;


namespace MRelayVisualizer
{
    public partial class Form1 : Form
    {
        
        public Form1()
        {
            InitializeComponent();
            LayoutEngine.InitLayout(this, BoundsSpecified.All);
            initR();
            Thread t1 = new Thread(new ThreadStart(this.initE));
            t1.Start();
    //        t2.Start();

            //    timer.Change(0, 300);
            Thread.Sleep(3000);
            timer = new System.Windows.Forms.Timer();
            timer.Tick += new EventHandler(updateGraph);
            
            timer.Interval = 2000;
            timer.Start();

        //    this.CreateGraph(zedGraphControl1);
        }
    }
}
