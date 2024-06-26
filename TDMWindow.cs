using System;
using System.Linq;
using vatsys;

namespace NATPlugin
{
    public partial class TDMWindow : BaseForm
    {
        public TDMWindow()
        {
            InitializeComponent();

            BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);

            Plugin.TracksUpdated += Plugin_TracksUpdated;
        }

        private void Plugin_TracksUpdated(object sender, EventArgs e)
        {
            DisplayTracks();
        }

        //private async void ButtonRefresh_Click(object sender, EventArgs e)
        //{
        //    _ = Plugin.GetTracks();
        //}

        private void NATWindow_Load(object sender, EventArgs e)
        {
            DisplayTracks();
        }

        private void DisplayTracks()
        {
            LabelTDM.Text = string.Empty;

            foreach (var track in Plugin.Tracks.OrderBy(x => x.Id))
            {
                LabelTDM.Text += $"{track.Id}\n {track.Start} - {track.End} \n {track.RouteDisplay} \n";
            }

        }
    }
}