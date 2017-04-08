using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Windows.Forms;
using ZzukBot.ExtensionFramework.Interfaces;

namespace Fisherman
{
    /// <summary>
    /// It is important to have all members of the interface set. Otherwise you may encounter some exceptions
    /// </summary>
    [Export(typeof(IBotBase))]
    public class Base : IBotBase
    {
        // This callback will be called once stop the botbase
        public Action OnStopCallback;
        // Tells wether the botbase is running right now
        private FishingLogic _logic;

        public void Dispose()
        {
            // Nothing to dispose
        }

        public bool Start(Action onStopCallback)
        {
            // Checking if we are already running
            if (_logic != null)
                // We are already running - return here and dont do anything further
                return false;
            // initialising new fishinglogic instace
            _logic = new FishingLogic(this);
            // Saving the callback to a private variable
            OnStopCallback = onStopCallback;
            _logic.Start();
            // return true (true means the botbase got started successfully)
            return true;
        }

        public void PauseBotbase(Action onPauseCallback)
        {
            // Pause functionality not needed. Leaving this empty
        }

        public bool ResumeBotbase()
        {
            // Resume functionality not needed. Leaving this empty
            return true;
        }

        public void Stop()
        {
            // if _logic is null the botbase isnt running   
            if (_logic == null) return;
            _logic.Stop();
            _logic = null;
        }

        public void ShowGui()
        {
            MessageBox.Show("This BotBase has no GUI");
        }

        public string Name { get; } = "Fisherman";
        public string Author { get; } = "Zzuk";
        public Version Version { get; } = new Version(0, 0, 0, 2);
    }
}
