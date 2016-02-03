using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pinger
{
    public partial class Form1 : Form
    {
        private BackgroundWorker bgWorker;
        private bool boolAlive = false;
        public bool boolBGMode = false;
        public bool boolWarningLock = false;
        public DateTime dateTime;
        public String strLastIcoState = "None";
        private SettingsForm settingsForm;

        private int intBGDelay = 3000;
        private int intAvgSample = 10;
        private int[] lstResults;
        private int intBack = 0;

        private int intAvg;
        private int intMin = -1;
        private int intMax = -1;

        public Form1()
        {
            InitializeComponent();
            bgWorker = new BackgroundWorker();

            dateTime = DateTime.Now;
            InitialiseAvgArray();
        }

        private void InitialiseAvgArray()
        {
            lstResults = new int[ intAvgSample ];
            PopulateArray( lstResults );
        }

        private void ChangeAvgArraySize( int intNewSize )
        {
            if( intNewSize == intAvgSample )
                return;

            int[] arrNew = new int[ intNewSize ];
            PopulateArray( arrNew );

            if( lstResults.Length > intNewSize )
            {
                Array.Resize<int>( ref lstResults, intNewSize );
                intAvgSample = intNewSize;
                return;
            }

            lstResults.CopyTo( arrNew, 0 );
            intAvgSample = intNewSize;
            lstResults = arrNew;
        }

        private void PopulateArray( int[] arr )
        {
            for( int i = 0; i < arr.Length; i++ )
                arr[ i ] = -1;
        }

        private void SendPing()
        {
            String strTime = String.Format( "%hh%mm%ss", DateTime.Now.TimeOfDay );

            AppendMain( "[" + DateTime.Now.ToString("hh:mm:ss") + "] Round Trip Time: " );

            if( !backgroundWorker1.IsBusy )
                backgroundWorker1.RunWorkerAsync();
        }

        private void backgroundWorker1_DoWork( object sender, DoWorkEventArgs e )
        {
            Ping pinger = new Ping();

            try
            {
                PingReply pingReplier = pinger.Send( "Google.com" );

                e.Result = ResolveReplyType( pingReplier );
            } catch( PingException )
            {
                e.Result = null;
            }
        }

        public PingStatus ResolveReplyType( PingReply pingReplier )
        {
            return new PingStatus( pingReplier );
        }

        private async void backgroundWorker1_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            if( boolAlive )
            {
                PingStatus status = (PingStatus) e.Result;

                if( status != null )
                {
                    AppendMain(
                        status.pingReply.Status == IPStatus.Success
                        ? status.pingReply.RoundtripTime + "\n"
                        : status.strStatus + "\n" );

                    AddResults( status.pingReply.Status == IPStatus.TimedOut
                        ? 5000
                        : (int) status.pingReply.RoundtripTime );

                    await Task.Delay( boolBGMode ? intBGDelay : 500 );
                }
                else
                {
                    AppendMain( "PINGING ERROR\n" );
                    AddResults( 5000 );
                    await Task.Delay( 5000 );
                }

                if( boolAlive )
                    SendPing();
                else
                    AppendMain( "### Safely Stopped Ping Loop ###\n" );
            }
            else 
                AppendMain( "\n### Safely Stopped Ping Loop ###\n" );
        }

        private void AddResults( int intResult )
        {
            lstResults[ intBack ] = intResult;
            intBack++;

            if( intBack >= intAvgSample )
                intBack = 0;

            UpdateData( intResult );
        }

        private void UpdateData( int intResult )
        {
            FindAverage();

            if( intResult > intMax )
            {
                intMax = intResult;
                lblMaxPing.Text = intMax.ToString();
            }
            else if( intMin == -1 || intResult < intMin )
            {
                intMin = intResult;
                lblMinPing.Text = intMin.ToString();
            }
        }

        private void FindAverage()
        {
            int sum = 0;
            int index = 0;
            int intNewAvg = 0;

            foreach( int result in lstResults )
            {
                if( result == -1 )
                    break;

                sum += result;
                index++;
            }

            if( index > 0 )
                intNewAvg = sum / index;
            
            if( intNewAvg != intAvg )
            {
                intAvg = intNewAvg;
                lblAvgPing.Text = intAvg.ToString();

                if( intAvg > 800 && strLastIcoState != "Bad" )
                    notifyIcon1.Icon = Pinger.Properties.Resources.pingericonB;
                else if( intAvg > 100 && intAvg <= 800 && strLastIcoState != "Med" )
                    notifyIcon1.Icon = Pinger.Properties.Resources.pingericonM;
                else if( intAvg <= 100 && strLastIcoState != "Good" )
                    notifyIcon1.Icon = Pinger.Properties.Resources.pingericonG;

                if( boolBGMode )
                {
                    float fSampleRatio = lstResults.Length == 0 
                        ? 0 
                        : ((float)index) / ((float) lstResults.Length);
                    Console.WriteLine( "Avg: " + intAvg );
                    
                    if( intAvg > 800 
                        && !boolWarningLock 
                        && ( fSampleRatio > 0.3 || index > 5 ) )
                    {
                        notifyIcon1.BalloonTipTitle = "Warning";
                        notifyIcon1.BalloonTipText = "[WARNING] The average ping is [" + intAvg
                            + "]" + " over the last [" + index + "] samples";
                        notifyIcon1.ShowBalloonTip( 2000 );
                        boolWarningLock = true;
                    }
                }

                if( intAvg < 100 && boolWarningLock )
                    boolWarningLock = false;
            }
        }

        private void AppendMain( String str )
        {
            txtMain.AppendText( str );
        }

        private void HandleStateChange( bool state )
        {
            if( state )
            {
                boolBGMode = true;
                notifyIcon1.Visible = true;
                notifyIcon1.BalloonTipTitle = "[Pinger]";
                notifyIcon1.BalloonTipText = "Pinger is testing connection in the background";
                notifyIcon1.ShowBalloonTip( 5000 );
                this.Hide();
            }
            else
            {
                boolBGMode = false;
                notifyIcon1.Visible = false;
            }
        }

        public void HandlePingToggle( bool state )
        {
            if( state && !boolAlive)
            {
                boolAlive = true;

                btnStopPing.ForeColor = Color.White;
                btnStartPing.ForeColor = Color.DimGray;
                AppendMain( "### Beginning Ping Loop ###\n" );
                SendPing();
            }
            else if( !state & boolAlive )
            {
                boolAlive = false;

                btnStopPing.ForeColor = Color.DimGray;
                btnStartPing.ForeColor = Color.White;
            }
        }
        private void Form1_Load( object sender, EventArgs e )
        {
            HandleStateChange( true );
            HandlePingToggle( true );
        }

        private void btnStartPing_Click( object sender, EventArgs e )
        {
            HandlePingToggle( true );
        }
   
        private void btnStopPing_Click( object sender, EventArgs e )
        {
            HandlePingToggle( false );   
        }

        private void notifyIcon1_MouseDoubleClick( object sender, MouseEventArgs e )
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void Form1_FormClosing( object sender, FormClosingEventArgs e )
        {
            notifyIcon1.Dispose();
        }

        private void button1_Click( object sender, EventArgs e )
        {
        }

        private void Form1_Resize( object sender, EventArgs e )
        {
            if( this.WindowState == FormWindowState.Minimized )
                HandleStateChange( true );
            else if( this.WindowState == FormWindowState.Normal )
                HandleStateChange( false );
        }
    }

    public class PingStatus
    {
        public PingReply pingReply;
        public String strStatus;

        public PingStatus( PingReply pingReply )
        {
            this.pingReply = pingReply;

            switch( pingReply.Status )
            {
                case IPStatus.BadDestination:
                    strStatus = "Bad Destination";
                    break;
                case IPStatus.DestinationHostUnreachable:
                    strStatus = "Host Unreachable";
                    break;
                case IPStatus.Success:
                    strStatus = "Success";
                    break;
                case IPStatus.TimedOut:
                    strStatus = "Timed Out";
                    break;
                case IPStatus.TimeExceeded:
                    strStatus = "Time Exceeded";
                    break;
                case IPStatus.Unknown:
                    strStatus = "Unknown Problem";
                    break;

            }
        }
    }
}
