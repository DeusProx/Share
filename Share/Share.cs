#define DEBUG
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{


    [Info("Share", "DeusProx", "0.1.0", ResourceId = 0000)]
    [Description("Share cupboards, codelocks and autoturrets")]
    public class Share : RustPlugin
    {
        #region Fields  
        [PluginReference]
        private Plugin PlayerDatabase;
        [PluginReference]
        private Plugin Friends;
        [PluginReference]
        private Plugin Clans;

        enum WantedEntityType : uint
        {
            AT = 0x0001,
            CL = 0x0002,
            CB = 0x0004,
            ALL = AT + CL + CB
        }

        private PluginConfig pluginConfig;

        private FieldInfo codelockwhitelist;
        #endregion

        #region Hooks
        void Loaded()
        {
            // Use string interpolation to format a float with 3 decimal points instead of calling string.Format()
            codelockwhitelist = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

            // Check if all dependencies are there
            Friends = plugins.Find("Friends");
            if (Friends == null)
                DebugMessage("Friends Plugin not found");
            else
                DebugMessage("Friends Plugin found");

            Clans = plugins.Find("Clans");
            if (Clans == null)
                DebugMessage("Clans Plugin not found");
            else
                DebugMessage("Clans Plugin found");

            // Load the config file
            LoadFromConfigFile();

            // Unsubscribe from Hooks if necessary
            if (!pluginConfig.General.ChangeOwnerIDOnCodeLockDeployed)
                Unsubscribe("OnItemDeployed");

            // Register Commands
            if (string.IsNullOrEmpty(pluginConfig.Commands.ShareCommand))
                DebugMessage("No valid ShareCommand in config.");
            else
                cmd.AddChatCommand(pluginConfig.Commands.ShareCommand, this, "cmdShareShort");

            if (string.IsNullOrEmpty(pluginConfig.Commands.UnshareCommand))
                DebugMessage("No valid UnshareCommand in config.");
            else
            {
                if (string.Equals(pluginConfig.Commands.ShareCommand, pluginConfig.Commands.UnshareCommand))
                    DebugMessage("ShareCommand & UnshareCommand are the same.");
                else
                    cmd.AddChatCommand(pluginConfig.Commands.UnshareCommand, this, "cmdShareShort");
            }
        }
        // Change OwnerID of entity when codelock is deployed
        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (entity & entity.HasSlot(BaseEntity.Slot.Lock) && entity.GetSlot(BaseEntity.Slot.Lock))
            {
                CodeLock cl = entity.GetSlot(BaseEntity.Slot.Lock).GetComponent<CodeLock>();
                if (cl)
                    entity.OwnerID = deployer.GetOwnerPlayer().userID;
            }
        }
        #endregion

        #region Configuration
        // Classes for easier handling of config
        class PluginConfig
        {
            public General General { get; set; }
            public Commands Commands { get; set; }
        }
        class General
        {
            public string ChatPrefix { get; set; }
            public bool UsePermission { get; set; }
            public string PermissionName { get; set; }
            public bool ChangeOwnerIDOnCodeLockDeployed { get; set; }
        }
        class Commands
        {
            public string ShareCommand { get; set; }
            public string UnshareCommand { get; set; }
            public bool AllowCupboardSharing { get; set; }
            public bool AllowCodelockSharing { get; set; }
            public bool AllowAutoturretSharing { get; set; }
            public float Radius { get; set; }
        }

        // Don't ever try to override SaveConfig() & LoadConfig()! Horrible idea!
        private void SaveToConfigFile() => Config.WriteObject(pluginConfig, true);
        private void LoadFromConfigFile() => pluginConfig = Config.ReadObject<PluginConfig>();

        // Creates default configuration file
        protected override void LoadDefaultConfig()
        {
            var defaultConfig = new PluginConfig
            {
                General = new General
                {
                    ChatPrefix = "<color=cyan>[Share]</color>",
                    UsePermission = false,
                    PermissionName = "share",
                    ChangeOwnerIDOnCodeLockDeployed = true
                },
                Commands = new Commands
                {
                    ShareCommand = "sh+",
                    UnshareCommand = "sh-",
                    AllowCupboardSharing = true,
                    AllowCodelockSharing = true,
                    AllowAutoturretSharing = true,
                    Radius = 100.0F
                }
            };
            Config.WriteObject(defaultConfig, true); // write into config file
        }
        #endregion

        /*#region Strange
        class ARPlayer
        {
            public string name;
            public ulong id;
            public BasePlayer basePlayer;

            public ARPlayer(ulong id, string name)
            {
                this.id = id;
                this.name = name;
                this.basePlayer = null;
            }

            public ARPlayer(BasePlayer bp)
            {
                name = bp.displayName;
                id = bp.userID;
                basePlayer = bp;
            }
        }
        #endregion*/

        #region Commands
        // if someone writes /share in the chat give him the help text
        [ChatCommand("share")]
        void cmdShare(BasePlayer player, string command, string[] args)
        {
            ShowCommandHelp(player);
            return;
        }

        void cmdShareShort(BasePlayer player, string command, string[] args)
        {
            DebugMessage("Called me!");
            // Check for right commands+arguments
            // TODO .....

            // Decide with who to share
            List<BasePlayer> playerList;
            switch (args[0].ToLower())
            {
                case "clan":
                    playerList = FindClanMember(player);
                    break;
                case "friends":
                    playerList = FindFriends(player);
                    break;
                default:
                    BasePlayer foundPlayer = FindPlayer(args[0]);
                    if (foundPlayer)
                    {
                        playerList = new List<BasePlayer>();
                        playerList.Add(foundPlayer);
                        break;
                    }
                    else
                    {
                        SendReply(player, "Player with name \"" + args[0] + "\" not found!");
                        return;
                    }
            }
            if (playerList != null)
                DebugMessage("Users on list: " + playerList.Count);
            else
                DebugMessage("No Players Found");

            DebugMessage("Before Items");
            // Check on what to auth
            // TDOD: argument could be null?
            List<BaseEntity>[] items;
            switch (args[1].ToLower())
            {
                case "at":
                    items = FindItems(player, pluginConfig.Commands.Radius, WantedEntityType.AT);
                    break;
                case "cl":
                    items = FindItems(player, pluginConfig.Commands.Radius, WantedEntityType.CL);
                    break;
                case "cb":
                    items = FindItems(player, pluginConfig.Commands.Radius, WantedEntityType.CB);
                    break;
                case "all":
                    items = FindItems(player, pluginConfig.Commands.Radius, WantedEntityType.ALL);
                    break;
                default:
                    return;
            }

            DebugMessage("items found:");
            DebugMessage("AT: " + items[0].Count);
            DebugMessage("CL: " + items[1].Count);
            DebugMessage("CB: " + items[2].Count);

            // Check whether to add or to remove
            if (string.Equals(command, pluginConfig.Commands.ShareCommand))
            {
                //share
            }
            else if (string.Equals(command, pluginConfig.Commands.UnshareCommand))
            {
                //unshare
            }
        }

        // Finds all entities a player owns on a certain radius & returns them
        private List<BaseEntity>[] FindItems(BasePlayer player, float radius, WantedEntityType entityMask)
        {
            Dictionary<int, int> checkedInstanceIDs = new Dictionary<int, int>();
            List<BaseEntity>[] foundItems = new List<BaseEntity>[3];

            int a = 0, b = 0, c = 0;
            foreach (var collider in Physics.OverlapSphere(player.transform.position, radius))
            {
                BaseEntity entity = collider.gameObject.ToBaseEntity();
                if (entity && !checkedInstanceIDs.ContainsKey(entity.GetInstanceID()))
                {
                    checkedInstanceIDs.Add(entity.GetInstanceID(), 1);
                    if (entity.OwnerID == player.userID)
                    {
                        if (IsBitSet(entityMask, WantedEntityType.AT) && entity is AutoTurret)
                        {
                            foundItems[0].Add(entity);
                        }
                        if (IsBitSet(entityMask, WantedEntityType.CL) && entity.HasSlot(BaseEntity.Slot.Lock) && entity.GetSlot(BaseEntity.Slot.Lock) && entity.GetSlot(BaseEntity.Slot.Lock).GetComponent<CodeLock>())
                        {
                            foundItems[1].Add(entity);
                        }
                        if (IsBitSet(entityMask, WantedEntityType.CB) && entity is BuildingPrivlidge)
                        {
                            foundItems[2].Add(entity);
                        }
                    }
                }

            }
            return foundItems;
        }

        bool IsBitSet(WantedEntityType value, WantedEntityType pos)
        {
            return (value & pos) != 0;
        }

        List<BasePlayer> FindFriends(BasePlayer player)
        {
            if (Friends == null)
                return null;

            List<BasePlayer> friends = new List<BasePlayer>();
            foreach (ulong userID in (ulong[])Friends?.Call("GetFriends", player.userID))
            {
                BasePlayer foundPlayer = FindPlayer(userID);
                if (foundPlayer)
                {
                    friends.Add(foundPlayer);
                }
            }

            return friends;
        }

        List<BasePlayer> FindClanMember(BasePlayer player)
        {
            if (Clans == null)
                return null;

            List<BasePlayer> clanMember = new List<BasePlayer>();
            string clanName = (string)Clans?.Call("GetClanOf", player.userID);

            if (string.IsNullOrEmpty(clanName))
                return null;
            else
            {
                JObject clan = (JObject)Clans?.Call("GetClan", clanName);
                if (clan != null)
                {
                    JArray members = (JArray)clan.GetValue("members");
                    if (members != null)
                    {
                        foreach (string member in members)
                        {
                            if (member == player.UserIDString)
                                continue;
                            BasePlayer foundPlayer = FindPlayer(member);
                            if (foundPlayer)
                            {
                                clanMember.Add(foundPlayer);
                            }
                        }
                    }
                }
            }

            return clanMember;
        }
        BasePlayer FindPlayer(string playerName)
        {
            BasePlayer foundPlayer = null;
            /*if (foundPlayer)
                return foundPlayer;

            foundPlayer = BasePlayer.FindSleeping(playerName);
            if (foundPlayer)
                return foundPlayer;*/

            IPlayer covplayer = covalence.Players.FindPlayer(playerName);
            if (covplayer != null)
                foundPlayer = (BasePlayer)covplayer.Object;

            return foundPlayer;
        }

        BasePlayer FindPlayer(ulong playerID)
        {
            BasePlayer foundPlayer = null;

            IPlayer covplayer = covalence.Players.FindPlayer(playerID.ToString());
            if (covplayer != null)
                foundPlayer = (BasePlayer)covplayer.Object;

            return foundPlayer;
        }

        //if(player.net.connection.authLevel < 2)
        //{
        //  SendReply(player, "You don´t have the permission to use this command.");
        //  return;
        //}

        /*if (args == null || args.Length < 2 || args.Length > 3)
        {
            ShowCommandHelp(player);
            return;
        }


        if (args.Length == 2 || args.Length == 3)
        {
            bCodelock = args[args.Length - 1].ToLower() == "cl" || args[args.Length - 1].ToLower() == "all";
            bCupboard = args[args.Length - 1].ToLower() == "cb" || args[args.Length - 1].ToLower() == "all";
            bAutoturret = args[args.Length - 1].ToLower() == "at" || args[args.Length - 1].ToLower() == "all";
        }*/

        /*if (!(bCodelock || bCupboard || bAutoturret))
        {
            ShowCommandHelp(player);
            return;
        }
        else if (args.Length == 2)
        {
            if (args[0].ToLower() == "show")
            {
                ShowItems(player, bCodelock, bCupboard, bAutoturret);
            }
            else if (args[0].ToLower() == "addfriends")
            {
                var foundPlayerList = FindFriendPlayers(player.userID);

                if (foundPlayerList == null || foundPlayerList.Count == 0)
                {
                    SendReply(player, "No friends found");
                    return;
                }

                if (foundPlayerList != null)
                {
                    var sb = new StringBuilder();
                    sb.Append("Found friends:");

                    foreach (var foundPlayer in foundPlayerList)
                    {
                        if (foundPlayer != null)
                            sb.Append(" " + foundPlayer.name);
                    }
                    PrintToConsole(player, sb.ToString());

                    int count = AddPlayersToWhitelists(player, foundPlayerList, bCodelock, bCupboard, bAutoturret);
                    SendReply(player, "Added Friends. Created " + count.ToString() + " whitelist entries");

                    ShowItems(player, bCodelock, bCupboard, bAutoturret);
                }
            }
            else if (args[0].ToLower() == "removefriends")
            {
                var foundPlayerList = FindFriendPlayers(player.userID);

                if (foundPlayerList == null || foundPlayerList.Count == 0)
                {
                    SendReply(player, "No friends found");
                    return;
                }

                if (foundPlayerList != null)
                {
                    var sb = new StringBuilder();
                    sb.Append("Found friends:");

                    foreach (var foundPlayer in foundPlayerList)
                    {
                        if (foundPlayer != null)
                            sb.Append(" " + foundPlayer.name);
                    }
                    PrintToConsole(player, sb.ToString());

                    int count = RemovePlayersFromWhitelists(player, foundPlayerList, bCodelock, bCupboard, bAutoturret);
                    SendReply(player, "Removed Friends. Deleted " + count.ToString() + " whitelist entries");

                    ShowItems(player, bCodelock, bCupboard, bAutoturret);
                }
            }
            else if (args[0].ToLower() == "addclan")
            {
                var foundPlayerList = FindClanPlayers(player.userID);

                if (foundPlayerList == null || foundPlayerList.Count == 0)
                {
                    SendReply(player, "No clan or clan members found");
                    return;
                }

                if (foundPlayerList != null)
                {
                    var sb = new StringBuilder();
                    sb.Append("Found clan members:");

                    foreach (var foundPlayer in foundPlayerList)
                    {
                        if (foundPlayer != null)
                            sb.Append(" " + foundPlayer.name);
                    }
                    PrintToConsole(player, sb.ToString());

                    int count = AddPlayersToWhitelists(player, foundPlayerList, bCodelock, bCupboard, bAutoturret);
                    SendReply(player, "Added Clan. Created " + count.ToString() + " whitelist entries");

                    ShowItems(player, bCodelock, bCupboard, bAutoturret);
                }
            }
            else if (args[0].ToLower() == "removeclan")
            {
                var foundPlayerList = FindClanPlayers(player.userID);

                if (foundPlayerList == null || foundPlayerList.Count == 0)
                {
                    SendReply(player, "No clan or clan members found");
                    return;
                }

                if (foundPlayerList != null)
                {
                    var sb = new StringBuilder();
                    sb.Append("Found clan members:");

                    foreach (var foundPlayer in foundPlayerList)
                    {
                        if (foundPlayer != null)
                            sb.Append(" " + foundPlayer.name);
                    }
                    PrintToConsole(player, sb.ToString());

                    int count = RemovePlayersFromWhitelists(player, foundPlayerList, bCodelock, bCupboard, bAutoturret);
                    SendReply(player, "Removed Clan. Deleted " + count.ToString() + " whitelist entries");

                    ShowItems(player, bCodelock, bCupboard, bAutoturret);
                }
            }
            else
                ShowCommandHelp(player);
        }
        else if (args.Length == 3)
        {
            if (args[0].ToLower() == "add")
            {
                ARPlayer foundPlayer = FindPlayer(args[1]);
                var foundPlayerList = new List<ARPlayer>();
                if (foundPlayer != null)
                {
                    foundPlayerList.Add(foundPlayer);
                }
                int count = AddPlayersToWhitelists(player, foundPlayerList, bCodelock, bCupboard, bAutoturret);
                SendReply(player, "Added player: " + foundPlayer.name + ". Created " + count.ToString() + " whitelist entries");
                ShowItems(player, bCodelock, bCupboard, bAutoturret);
            }
            else if (args[0].ToLower() == "remove")
            {
                ARPlayer foundPlayer = FindPlayer(args[1]);
                var foundPlayerList = new List<ARPlayer>();
                if (foundPlayer != null)
                {
                    foundPlayerList.Add(foundPlayer);
                }

                int count = RemovePlayersFromWhitelists(player, foundPlayerList, bCodelock, bCupboard, bAutoturret);
                SendReply(player, "Removed player: " + foundPlayer.name + ". Deleted " + count.ToString() + " whitelist entries");
                ShowItems(player, bCodelock, bCupboard, bAutoturret);
            }
            else
                ShowCommandHelp(player);
        }
        else
        {
            ShowCommandHelp(player);
        }

    }*/

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        [HookMethod("SendHelpText")]
        private void ShowCommandHelp(BasePlayer player)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=16>Share</size> by DeusProx");
            sb.AppendLine("<size=12>Shares items with other players in a " + pluginConfig.Commands.Radius + "m radius around you.</size>");
            sb.AppendLine("<size=1> </size>");

            sb.AppendLine("<color=#FFD479>/" + pluginConfig.Commands.ShareCommand + " <who> <what></color>");
            sb.AppendLine("<size=12>Shares the item <what> with every player <who></size>");
            sb.AppendLine("<color=#FFD479>/" + pluginConfig.Commands.UnshareCommand + " <who> <what></color>");
            sb.AppendLine("<size=12>Unshares the item <what> with every player <who></size>");
            sb.AppendLine("<size=1> </size>");

            sb.AppendLine("<color=#FFD479><who></color><size=12> can be <color=orange>clan</color>, <color=orange>friends</color> or a player name</size>");
            sb.AppendLine("<color=#FFD479><what></color><size=12> can be <color=orange>at</color>(AutoTurrets), <color=orange>cl</color>(Codelocks), <color=orange>cb</color>(Cupboards) or <color=orange>all</color></size>");
            sb.AppendLine("<size=12>Example: <color=#FFD479>/" + pluginConfig.Commands.ShareCommand + " \"Ser Winter\" all</color></size>");

            SendReply(player, sb.ToString());
        }
        #endregion

        /*#region Functions
        void ShowItems(BasePlayer player, bool bCodelock, bool bCupboard, bool bAutoturret)
        {
            var cupboards = new Dictionary<int, BuildingPrivlidge>();
            var codelocks = new Dictionary<int, CodeLock>();
            var autoturrets = new Dictionary<int, AutoTurret>();

            FindItemsToRegister(player, pluginConfig.Commands.Radius, bCodelock, bCupboard, bAutoturret, cupboards, codelocks, autoturrets);

            var sbChat = new StringBuilder();
            var sbConsole = new StringBuilder();

            if (bCupboard)
            {
                sbChat.AppendLine("Found " + cupboards.Count + " Cupboards");
                sbConsole.AppendLine("Found " + cupboards.Count + " Cupboards");
                foreach (var cb in cupboards)
                {
                    sbConsole.AppendLine("Cupboard: " + cb.Key.ToString());
                    foreach (var regplayer in GetWhitelist(cb.Value))
                    {
                        sbConsole.AppendLine("  -" + regplayer.name);
                    }
                    //sbConsole.AppendLine();
                }
            }

            if (bCodelock)
            {
                sbChat.AppendLine("Found " + codelocks.Count + " Code Locks");
                //sbConsole.AppendLine();
                sbConsole.AppendLine("Found " + codelocks.Count + " Code Locks");

                foreach (var cl in codelocks)
                {
                    sbConsole.AppendLine("Code Lock: " + cl.Key.ToString());
                    foreach (var regplayer in GetWhitelist(cl.Value))
                    {
                        sbConsole.AppendLine("  -" + regplayer.name);
                    }
                    //sbConsole.AppendLine();
                }
            }

            if (bAutoturret)
            {
                sbChat.AppendLine("Found " + autoturrets.Count + " Auto Turrets");
                //sbConsole.AppendLine();
                sbConsole.AppendLine("Found " + autoturrets.Count + " Auto Turrets");

                foreach (var at in autoturrets)
                {
                    sbConsole.AppendLine("Auto Turret: " + at.Key.ToString());
                    foreach (var regplayer in GetWhitelist(at.Value))
                    {
                        sbConsole.AppendLine("  -" + regplayer.name);
                    }
                    //sbConsole.AppendLine();
                }
            }

            sbChat.AppendLine("For details see the console");
            SendReply(player, sbChat.ToString());
            PrintToConsole(player, sbConsole.ToString());
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        int AddPlayersToWhitelists(BasePlayer player, List<ARPlayer> addPlayerList, bool bCodelock, bool bCupboard, bool bAutoturret)
        {
            var cupboards = new Dictionary<int, BuildingPrivlidge>();
            var codelocks = new Dictionary<int, CodeLock>();
            var autoturrets = new Dictionary<int, AutoTurret>();

            int count = 0;
            FindItemsToRegister(player, pluginConfig.Commands.Radius, bCodelock, bCupboard, bAutoturret, cupboards, codelocks, autoturrets);

            foreach (var foundPlayer in addPlayerList)
            {
                if (foundPlayer == null)
                    continue;

                foreach (BuildingPrivlidge cb in cupboards.Values)
                {
                    if (AddToWhitelist(cb, foundPlayer))
                        count++;
                }

                foreach (CodeLock cl in codelocks.Values)
                {
                    if (AddToWhitelist(cl, foundPlayer))
                        count++;
                }

                foreach (AutoTurret at in autoturrets.Values)
                {
                    if (AddToWhitelist(at, foundPlayer))
                        count++;
                }
            }

            return count;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        int RemovePlayersFromWhitelists(BasePlayer player, List<ARPlayer> removePlayerList, bool bCodelock, bool bCupboard, bool bAutoturret)
        {
            var cupboards = new Dictionary<int, BuildingPrivlidge>();
            var codelocks = new Dictionary<int, CodeLock>();
            var autoturrets = new Dictionary<int, AutoTurret>();

            int count = 0;
            FindItemsToRegister(player, pluginConfig.Commands.Radius, bCodelock, bCupboard, bAutoturret, cupboards, codelocks, autoturrets);

            foreach (var foundPlayer in removePlayerList)
            {
                if (foundPlayer == null)
                    continue;

                foreach (BuildingPrivlidge cb in cupboards.Values)
                {
                    if (RemoveFromWhitelist(cb, foundPlayer))
                        count++;
                }

                foreach (CodeLock cl in codelocks.Values)
                {
                    if (RemoveFromWhitelist(cl, foundPlayer))
                        count++;
                }

                foreach (AutoTurret at in autoturrets.Values)
                {
                    if (RemoveFromWhitelist(at, foundPlayer))
                        count++;
                }
            }
            return count;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        private static void FindItemsToRegister(BasePlayer player, float radius, bool bCodelock, bool bCupboard, bool bAutoturret, Dictionary<int, BuildingPrivlidge> cupboards, Dictionary<int, CodeLock> codelocks, Dictionary<int, AutoTurret> autoturrets)
        {
            Collider[] hitColliders = Physics.OverlapSphere(player.transform.position, radius);
            foreach (Collider hitCollider in hitColliders)
            {
                BaseEntity baseEntity = hitCollider.gameObject.ToBaseEntity();
                if (baseEntity && baseEntity.OwnerID == player.userID)
                {
                    if (bCodelock && baseEntity.HasSlot(BaseEntity.Slot.Lock))
                    {
                        BaseEntity slotentity = baseEntity.GetSlot(BaseEntity.Slot.Lock);
                        if (slotentity != null)
                        {
                            CodeLock codelock = slotentity.GetComponent<CodeLock>();
                            if (codelock != null)
                            {
                                if (!codelocks.ContainsKey(codelock.GetInstanceID()))
                                {
                                    codelocks.Add(codelock.GetInstanceID(), codelock);
                                }
                            }
                        }
                    }

                    if (bCupboard && baseEntity is BuildingPrivlidge)
                    {
                        if (!cupboards.ContainsKey(baseEntity.GetInstanceID()))
                            cupboards.Add(baseEntity.GetInstanceID(), baseEntity as BuildingPrivlidge);
                    }

                    if (bAutoturret && baseEntity is AutoTurret)
                    {
                        if (!autoturrets.ContainsKey(baseEntity.GetInstanceID()))
                            autoturrets.Add(baseEntity.GetInstanceID(), baseEntity as AutoTurret);
                    }
                }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        List<ARPlayer> GetWhitelist(BuildingPrivlidge cupboard)
        {
            List<ARPlayer> whitelistplayers = new List<ARPlayer>();
            foreach (var playerID in cupboard.authorizedPlayers)
            {
                if (FindPlayer(playerID.userid) != null)
                    whitelistplayers.Add(FindPlayer(playerID.userid));
            }

            return whitelistplayers;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        bool AddToWhitelist(BuildingPrivlidge cupboard, ARPlayer addPlayer)
        {
            var protoPlayer = new ProtoBuf.PlayerNameID();
            protoPlayer.userid = addPlayer.id;
            protoPlayer.username = addPlayer.name;

            foreach (var whitelistplayer in cupboard.authorizedPlayers)
            {
                if (whitelistplayer.userid == addPlayer.id)
                    return false;
            }

            cupboard.authorizedPlayers.Add(protoPlayer);
            cupboard.SendNetworkUpdate();

            if (addPlayer.basePlayer != null && cupboard.CheckEntity(addPlayer.basePlayer))
            {
                addPlayer.basePlayer.SetInsideBuildingPrivilege(cupboard, true);
            }

            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        bool RemoveFromWhitelist(BuildingPrivlidge cupboard, ARPlayer removePlayer)
        {
            bool removed = false;
            var protoListArray = cupboard.authorizedPlayers.ToArray();

            for (int index = protoListArray.GetLength(0) - 1; index >= 0; index--)
            {
                if (protoListArray[index].userid == removePlayer.id)
                {
                    cupboard.authorizedPlayers.RemoveAt(index);
                    cupboard.SendNetworkUpdate();

                    if (removePlayer.basePlayer != null && cupboard.CheckEntity(removePlayer.basePlayer))
                    {
                        removePlayer.basePlayer.SetInsideBuildingPrivilege(cupboard, false);
                    }

                    removed = true;
                }
            }

            return removed;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        List<ARPlayer> GetWhitelist(CodeLock codelock)
        {
            List<ulong> whitelisted = codelockwhitelist.GetValue(codelock) as List<ulong>;
            List<ARPlayer> whitelistplayers = new List<ARPlayer>();
            foreach (ulong playerID in whitelisted)
            {
                if (FindPlayer(playerID) != null)
                    whitelistplayers.Add(FindPlayer(playerID));
            }

            return whitelistplayers;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        bool AddToWhitelist(CodeLock codelock, ARPlayer addPlayer)
        {
            List<ulong> whitelisted = codelockwhitelist.GetValue(codelock) as List<ulong>;

            if (whitelisted.Contains(addPlayer.id))
            {
                return false;
            }
            else
            {
                whitelisted.Add(addPlayer.id);
                codelockwhitelist.SetValue(codelock, whitelisted);
                codelock.SendNetworkUpdate();
                return true;
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        bool RemoveFromWhitelist(CodeLock codelock, ARPlayer removePlayer)
        {
            List<ulong> whitelisted = codelockwhitelist.GetValue(codelock) as List<ulong>;

            if (whitelisted.Contains(removePlayer.id))
            {
                whitelisted.Remove(removePlayer.id);
                codelockwhitelist.SetValue(codelock, whitelisted);
                codelock.SendNetworkUpdate();

                return true;
            }
            else
            {
                return false;
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        List<ARPlayer> GetWhitelist(AutoTurret turret)
        {
            List<ARPlayer> whitelistplayers = new List<ARPlayer>();
            foreach (var playerID in turret.authorizedPlayers)
            {
                if (FindPlayer(playerID.userid) != null)
                    whitelistplayers.Add(FindPlayer(playerID.userid));
            }

            return whitelistplayers;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        bool AddToWhitelist(AutoTurret turret, ARPlayer addPlayer)
        {
            var protoPlayer = new ProtoBuf.PlayerNameID();
            protoPlayer.userid = addPlayer.id;
            protoPlayer.username = addPlayer.name;

            foreach (var whitelistplayer in turret.authorizedPlayers)
            {
                if (whitelistplayer.userid == addPlayer.id)
                    return false;
            }
            turret.authorizedPlayers.Add(protoPlayer);
            turret.SendNetworkUpdate();
            turret.SetTarget(null);

            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        bool RemoveFromWhitelist(AutoTurret turret, ARPlayer removePlayer)
        {
            bool removed = false;
            var protoListArray = turret.authorizedPlayers.ToArray();

            for (int index = protoListArray.GetLength(0) - 1; index >= 0; index--)
            {
                if (protoListArray[index].userid == removePlayer.id)
                {
                    turret.authorizedPlayers.RemoveAt(index);
                    turret.SendNetworkUpdate();
                    turret.SetTarget(null);
                    removed = true;
                }
            }
            return removed;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        List<ARPlayer> FindFriendPlayers(ulong playerID)
        {
            if (Friends == null)
                return null;

            var foundPlayerList = new List<ARPlayer>();
            var friends = (ulong[])Friends?.Call("GetFriends", playerID);


            foreach (var friendID in friends)
            {
                ARPlayer foundPlayer = FindPlayer(friendID);
                if (foundPlayer != null)
                {
                    foundPlayerList.Add(foundPlayer);
                }
            }

            return foundPlayerList;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        List<ARPlayer> FindClanPlayers(ulong playerID)
        {
            if (Clans == null)
            {
                return null;
            }

            var foundPlayerList = new List<ARPlayer>();
            string clanName = (string)Clans.Call("GetClanOf", playerID);

            if (clanName != null)
            {
                var clan = Clans.Call("GetClan", clanName);
                if (clan != null && clan is JObject)
                {
                    var members = (clan as JObject).GetValue("members");
                    if (members != null && members is JArray)
                    {
                        foreach (string member in (JArray)members)
                        {
                            if (member == playerID.ToString())
                                continue;
                            ARPlayer foundPlayer = FindPlayer(member);
                            if (foundPlayer != null)
                            {
                                foundPlayerList.Add(foundPlayer);
                            }
                        }
                    }
                }
            }

            return foundPlayerList;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        ARPlayer FindPlayer(string playerName)
        {
            ARPlayer foundPlayer = null;
            BasePlayer foundBasePlayer = BasePlayer.Find(playerName);

            if (!foundBasePlayer)
                foundBasePlayer = BasePlayer.FindSleeping(playerName);

            if (!foundBasePlayer) // find offline or dead
            {
                IPlayer covplayer = covalence.Players.FindPlayer(playerName);
                if (covplayer != null)
                    foundBasePlayer = (BasePlayer)covplayer.Object;
            }

            if (foundBasePlayer != null)
                foundPlayer = new ARPlayer(foundBasePlayer);

            return foundPlayer;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// 
        //////////////////////////////////////////////////////////////////////////////////////////
        ARPlayer FindPlayer(ulong playerID)
        {
            ARPlayer foundPlayer = null;
            BasePlayer foundBasePlayer = BasePlayer.FindByID(playerID);

            if (!foundBasePlayer)
                foundBasePlayer = BasePlayer.FindSleeping(playerID);

            if (!foundBasePlayer) // find offline or dead
            {
                IPlayer covplayer = covalence.Players.FindPlayer(playerID.ToString());
                if (covplayer != null)
                    foundBasePlayer = (BasePlayer)covplayer.Object;
            }

            if (foundBasePlayer != null)
                foundPlayer = new ARPlayer(foundBasePlayer);


            if (PlayerDatabase != null && foundPlayer == null)
            {
                string strunknown = "Unknown: " + playerID.ToString();
                //string strunknown = "Unknown";
                var name = (string)PlayerDatabase?.Call("GetPlayerData", playerID.ToString(), "name") ?? strunknown;
                if (name != null)
                {
                    foundPlayer = new ARPlayer(playerID, name);
                }
            }

            if (foundPlayer == null)
            {
                foundPlayer = new ARPlayer(playerID, "Unknown: " + playerID);
                //foundPlayer = new ARPlayer(playerID, "Unknown");
                //DebugMessage("Player not found for ID: " + playerID.ToString());
            }

            return foundPlayer;
        }
        #endregion*/

        #region Messages
        public void DebugMessage(string msg) { Debug.Log("[Share] " + msg); }
        #endregion
    }
}