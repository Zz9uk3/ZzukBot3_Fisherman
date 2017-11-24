using System;
using System.Linq;
using System.Windows.Forms;
using ZzukBot.Game.Statics;
using ZzukBot.Mem;
using ZzukBot.Objects;
using FluentBehaviourTree;
using ZzukBot.Game.Frames;

namespace Fisherman
{
    public class FishingLogic
    {
        public volatile bool _isRunning;
        // BehaviorTree logic
        private IBehaviourTreeNode _tree;
        private readonly object _lock;
        // Store a reference to the Base instance so we can stop the botbase from here
        private readonly Base _baseInstance;
        // The current bober object
        private WoWGameObject _bobber;
        // after looting a bobber it will fade from the players eyes however the object might stay in memory longer
        // obviously we want to ignore the "invalid" bobber object
        // here we will store the guid of the "invalid" bobber
        private ulong _oldBobberGuid;
        private readonly MainThread.Updater _logicPulse;

        private string startLocation;
        private string[] _lures = { "WTFISGOINGON??", "Shiny Bauble", "Nightcrawlers", "Bright Baubles", "Aquadynamic Fish Attractor" };

        // Constructor
        public FishingLogic(Base baseInstance)
        {
            _baseInstance = baseInstance;
            _lock = new object();
            // Creating an updater instace which will execute LogicPulse each 250ms from the mainthread
            _logicPulse = new MainThread.Updater(LogicPulse, 250);

            // Build the BehaviorTree which executes the logic
            // Sequence: Will jump to the next Do if the current Do returns success
            var builder = new BehaviourTreeBuilder();
            _tree = builder.Sequence("Fishing")
                .Sequence("Fishing")
                .Do("CheckIngame", data =>
                {
                    if (!ObjectManager.Instance.IsIngame) return BehaviourTreeStatus.Failure;
                    var player = ObjectManager.Instance.Player;
                    // We are ingame and we find the player = success
                    return player == null ? BehaviourTreeStatus.Failure : BehaviourTreeStatus.Success;
                })
                .Do("AntiTP", data =>
                {
                    var player = ObjectManager.Instance.Player;
                    if (player.RealZoneText != startLocation)
                    {
                        Console.Beep(800, 2000);
                        Stop();
                        return BehaviourTreeStatus.Failure;
                    }
                    return BehaviourTreeStatus.Success;
                })
                .Do("CheckLootWindow", data =>
                {
                    var player = ObjectManager.Instance.Player;
                    // Loot window closed? Continue
                    if (player.CurrentLootGuid == 0 || !LootFrame.IsOpen) return BehaviourTreeStatus.Success;
                    // Otherwise try to loot, blacklist the current bobber and keep repeating this Do
                    _oldBobberGuid = player.CurrentLootGuid;
                    if (ZzukBot.Helpers.Wait.For("FishingLootWait2", 500))
                        LootFrame.Instance.LootAll();
                    return BehaviourTreeStatus.Running;
                })
                .Do("ApplyLure", data =>
                {
                    var player = ObjectManager.Instance.Player;
                    var lure = Inventory.Instance.GetLastItem(_lures);
                    var bobber = ObjectManager.Instance.GameObjects.FirstOrDefault(x => x.OwnedBy == player.Guid && x.Guid != _oldBobberGuid);
                    if ((player.Channeling != 0 && bobber != null) || player.IsMainhandEnchanted() || lure == "")
                    {
                        return BehaviourTreeStatus.Success;
                    }
                    if (!player.IsMainhandEnchanted() && ZzukBot.Helpers.Wait.For("ApplyingLure", 5500))
                    {
                        player.EnchantMainhandItem(lure);
                    }
                    return BehaviourTreeStatus.Running;
                })
                .Do("Cast", data =>
                {
                    var player = ObjectManager.Instance.Player;
                    // We are channeling something (fishing)? Success!
                    var bobber = ObjectManager.Instance.GameObjects.FirstOrDefault(x => x.OwnedBy == player.Guid && x.Guid != _oldBobberGuid);
                    if (player.Channeling != 0 && bobber != null) return BehaviourTreeStatus.Success;
                    // Cast fishing (only each 1000ms tho)
                    if (ZzukBot.Helpers.Wait.For("FishingWait", 1000))
                        Spell.Instance.Cast("Fishing");
                    ObjectManager.Instance.Player.AntiAfk();
                    // Stil running. Only success if we are casting fishing
                    return BehaviourTreeStatus.Running;
                })
                .Do("WaitForFish", data =>
                {
                    var player = ObjectManager.Instance.Player;
                    var bobber = ObjectManager.Instance.GameObjects.FirstOrDefault(x => x.OwnedBy == player.Guid && x.Guid != _oldBobberGuid);
                    // No bobber? Fail
                    if (bobber == null) return BehaviourTreeStatus.Failure;
                    // Bobber got no fish? Waiting!
                    if (!bobber.IsBobbing) return BehaviourTreeStatus.Running;
                    // Got a fish on bobber? Loot it and add the guid of the bobber to the blacklist
                    if (ZzukBot.Helpers.Wait.For("FishingLootWait", 500))
                        bobber.Interact(false);
                    return BehaviourTreeStatus.Success;
                })
                .End()
                .Build();
        }

        private void LogicPulse()
        {
            _tree.Tick(new TimeData());
            // If we are stil running we want to return here
            if (_isRunning) return;
            _logicPulse.Stop();
            // Calling the onstop callback to signalise the core that we are done running the botbase
            _baseInstance.OnStopCallback();
        }

        // Stopping the botbase
        public void Stop()
        {
            // setting _isRunning to false will break out of the logic
            _isRunning = false;
        }

        public bool Start()
        {
            // only one thread can tap into the lock at a time (preventing multiple starts etc.)
            lock (_lock)
            {
                if (_isRunning) return false;

                // If we are not ingame we dont want to run the bot
                if (!ObjectManager.Instance.IsIngame)
                {
                    MessageBox.Show("Please make sure to be ingame before starting Fisherman");
                    return false;
                }
                var player = ObjectManager.Instance.Player;
                // If we cant get the player object we dont want to run the bot
                if (player == null)
                {
                    MessageBox.Show("Couldnt find the player object");
                    return false;
                }
                // No fishing skill = no botting
                if (Spell.Instance.GetSpellRank("Fishing") == 0)
                {
                    MessageBox.Show("The active toon has no fishing skill");
                    return false;
                }

                startLocation = player.RealZoneText;

                // All checks passed. Lets run the botbase
                _isRunning = true;
            }
            _logicPulse.Start();
            return _isRunning;
        }
    }
}
