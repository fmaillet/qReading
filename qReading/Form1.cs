using System;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tobii.Interaction;
using Tobii.Interaction.Framework;
using MySql.Data.MySqlClient;
using System.Net.NetworkInformation;
using System.Threading;

namespace qReading
{
    public partial class mainForm : Form
    {
        public List<string> words;
        //Boolean connected = false;
        string macAddr = "";
        string titre = "";
        string name = "";
        string prenom = "";
        Thread autoConnect;

        string MyConnectionString = "Server=fredericmaillet.fr;Database=fmaillet_professionnels;Uid=fmaillet_fredo;Pwd=mastercog;";

        static private Host eyeX = new Host();
        //FixationDataStream fixationDataStream;
        //private static HeadPoseStream _headPoseStream;

        SpeechSynthesizer synth;
        SpeechRecognitionEngine _recognizer;
        PromptBuilder consignes, falseRecog;
        
        public mainForm()
        {
            InitializeComponent();
            
            //Get words list
            string resource_data = Properties.Resources.mots;
            words = resource_data.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
            System.Console.WriteLine(words[0]);

            //Get MacAddress (required for auto connect)
            foreach  (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                macAddr = string.Join("-", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                Console.WriteLine("macAddress: " + macAddr);
                break;  //get only first one
            }
            //Launch autoConnect thread
            autoConnect = new Thread(new ThreadStart(autoConnectThread));
            autoConnect.Start();

            //Initialize speech synth
            synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();

            //Initializing eyeX
            eyeX = new Host();
            var userPresenceStateObserver = eyeX.States.CreateUserPresenceObserver();
            userPresenceStateObserver.WhenChanged(userPresenceState =>
            {
                if (userPresenceState.IsValid)
                    switch (userPresenceState.Value)
                    {
                        case UserPresence.Present:
                            Console.WriteLine("User is present");
                            allowEyeTrack(true);
                            break;

                        default:
                            Console.WriteLine("User is not present");
                            allowEyeTrack(false);
                            break;
                    }
            });
        }

        //Observable connected boolean var
        private Boolean _myConnected = false;
        delegate void StringArgReturningVoidDelegate(string text);
        public Boolean MyConnected
        {
            get { return _myConnected; }
            set
            {
                _myConnected = value;
                if (_myConnected == true)
                {
                    this.SetText ("qReading - Connecté : " + titre + " " + prenom + " " + name);
                    this.connexionServeurToolStripMenuItem.Enabled = false;
                }
                else
                {
                    this.SetText ("qReading - MODE DEMO (NON CONNECTE)");
                    this.connexionServeurToolStripMenuItem.Enabled = true;
                }
            }
        }

        //Just to set the form title thread safe
        private void SetText(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.InvokeRequired)
            {
                StringArgReturningVoidDelegate d = new StringArgReturningVoidDelegate(SetText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.Text = text;
            }
        }

        //Same for menu item
        private void SetMenuItemEnabled(Boolean b)
        {
            if (this.InvokeRequired)
            {

            }
        }

        //Auto Connect thread from MacAddress
        private void autoConnectThread()
        {
            //Test mySQL connection
            MySqlConnection connection = new MySqlConnection(MyConnectionString);
            connection.Open();
            MySqlCommand cmd;
            MySqlDataReader reader = null;
            try
            {
                cmd = connection.CreateCommand();
                cmd.CommandText = "select * from Pro where MACADR = '" + macAddr + "'";
                Console.WriteLine(cmd.CommandText);

                reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    titre = reader.GetString("TITRE");
                    name = reader.GetString("NOM");
                    prenom = reader.GetString("PRENOM");
                    MyConnected = true;
                }
                if (MyConnected == false) MyConnected = false;
            }
            catch (Exception)
            {
                Console.WriteLine("mySql error");
                throw;
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                {
                    Console.WriteLine("mySql com open");
                    connection.Close();
                }
            }
        }


        //Quit application (from menu)
        private void quitterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.Application.Exit();
        }

        //Allow eye-tracking
        private void allowEyeTrack (Boolean b)
        {
            Invoke(new Action(() =>
            {
                calButton.Enabled = b;
            }));
            
        }

        //Compute age from birth date
        private void birthDate_ValueChanged(object sender, EventArgs e)
        {
            //check for today max
            DateTime bd = this.birthDate.Value.Date;
            DateTime dt = DateTime.Today;
            if (DateTime.Compare(bd, dt) >= 0)
            {
                birthDate.Value = dt;
                return;
            }

            int y = this.birthDate.Value.Date.Year;
            int m = this.birthDate.Value.Date.Month;
            int d = this.birthDate.Value.Date.Day;
            int dd = DateTime.Today.Day;
            int mm = DateTime.Today.Month;
            int yy = DateTime.Today.Year;

            // Calc age day
            if (dd<d)
            {
                dd = dd + 30;
                mm = mm - 1;
            }
            int ad = dd - d;
            //calc age month
            if (mm < m)
            {
                mm = mm + 1;
                yy = yy - 1;
            }
            int am = mm - m;
            //calc age year
            int ay = yy - y;
            //set label
            string st = ay + " ans " + am + " mois (" + ad + " jours)";
            this.ageLabel.Text = st;
        }

        //Launch eye-tracker calibration
        private void button1_Click(object sender, EventArgs e)
        {
            //eyeX.Context.LaunchConfigurationTool(ConfigurationTool.RetailCalibration, (data) => { });
            eyeX.Context.LaunchConfigurationTool(ConfigurationTool.Recalibrate, (data) => { });
        }

        private void connexionServeurToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Connexion serveur");
        }

        //Check for speech synthesis
        private void speechButton_Click(object sender, EventArgs e)
        {
            consignes = new PromptBuilder();

            consignes.StartSentence();
            consignes.AppendText("Ceci est un essai du système de synthèse vocale.");
            //consignes.StartStyle(new PromptStyle(PromptEmphasis.Moderate));
            //consignes.AppendText("système de synthèse vocale");
            //consignes.EndStyle();
            //consignes.AppendText(" le plus vite possible.");
            consignes.EndSentence();

            synth.SpeakAsync(consignes);
        }
    }
}
