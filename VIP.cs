using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Menu;
using static CounterStrikeSharp.API.Core.Listeners;
using System.Runtime.Intrinsics.Arm;
using static System.Runtime.InteropServices.JavaScript.JSType;

using Nexd.MySQL;
using System.Runtime.ExceptionServices;
using CounterStrikeSharp.API.Core.Attributes;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Entities;

namespace VIP;
[MinimumApiVersion(55)]
public class ConfigVIP : BasePluginConfig
{
    [JsonPropertyName("Prefix")] public string Prefix { get; set; } = $" {ChatColors.Default}[{ChatColors.Green}MadGames.eu{ChatColors.Default}]";
    [JsonPropertyName("GiveHPAfterKill")] public bool GiveHPAfterKill { get; set; } = true;
    [JsonPropertyName("GiveMoneyAfterKill")] public bool GiveMoneyAfterKill { get; set; } = true;
    [JsonPropertyName("EnableVIPPrefix")] public bool EnableVIPPrefix { get; set; } = true;
    [JsonPropertyName("EnableVIPAcceries")] public bool EnableVIPAcceries { get; set; } = true;
    [JsonPropertyName("EnableVIPColoredSmokes")] public bool EnableVIPColoredSmokes { get; set; } = true;
    [JsonPropertyName("KnifeDMGEnable")] public bool KnifeDMGEnable { get; set; } = false;
    [JsonPropertyName("WelcomeMessageEnable")] public bool WelcomeMessageEnable { get; set; } = true;
    [JsonPropertyName("ReservedSlotsForVIP")] public int ReservedSlotsForVIP { get; set; } = 1;

    [JsonPropertyName("WelcomeMessage")] public string WelcomeMessage { get; set; } = $"Welcom on server you are BEST VIP!";
    [JsonPropertyName("DBDatabase")] public string DBDatabase { get; set; } = "database";
    [JsonPropertyName("DBUser")] public string DBUser { get; set; } = "user";
    [JsonPropertyName("DBPassword")] public string DBPassword { get; set; } = "password";
    [JsonPropertyName("DBHost")] public string DBHost { get; set; } = "localhost";
    [JsonPropertyName("DBPort")] public int DBPort { get; set; } = 3306;

    [JsonPropertyName("translation")]
    public TranslationClass TranslationClass { get; set; } = new TranslationClass();

}
public class TranslationClass
{
    [JsonPropertyName("OnceUse")] public string OnceUse { get; set; } = $" This command you can use {ChatColors.Red}only once{ChatColors.Default} on round!";
    [JsonPropertyName("MustBeVIP")] public string MustBeVIP { get; set; } = $" This command are allowed only for {ChatColors.Lime}VIP{ChatColors.Default}!";
    [JsonPropertyName("MustBeThird")] public string MustBeThird { get; set; } = $" Must be a {ChatColors.Red}Third{ChatColors.Default} round, to use this command!";



}
public static class GetUnixTime
{
    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() -
                       new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (int)unixTime.TotalSeconds;
    }
}
public partial class VIP : BasePlugin, IPluginConfig<ConfigVIP>
{
    public override string ModuleName => "VIP";
    public override string ModuleAuthor => "DeadSwim";
    public override string ModuleDescription => "Simple VIP system based on database.";
    public override string ModuleVersion => "V. 1.0.3";
    private string DatabaseConnectionString = string.Empty;
    private static readonly int?[] IsVIP = new int?[65];
    private static readonly int?[] Used = new int?[65];
    private static readonly int?[] LastUsed = new int?[65];

    private static readonly int[] J = new int[Server.MaxPlayers];
    private static readonly PlayerFlags[] LF = new PlayerFlags[Server.MaxPlayers];
    private static readonly PlayerButtons[] LB = new PlayerButtons[Server.MaxPlayers];

    public ConfigVIP Config { get; set; }

    public int Round;
    public int ConnectedPlayers;


    public void OnConfigParsed(ConfigVIP config)
    {
        Config = config;
        if (config.DBHost.Length < 1 || Config.DBUser.Length < 1 || Config.DBPassword.Length < 1)
        {
            throw new Exception("You need to setup Database credentials in config!");
        }
    }
    private bool IsInt(string sVal)
    {
        foreach (char c in sVal)
        {
            int iN = (int)c;
            if ((iN > 57) || (iN < 48))
                return false;
        }
        return true;
    }


    public override void Load(bool hotReload)
    {
        Console.WriteLine($"{Config.Prefix} VIP Plugins started, by deadswim");
        try
        {
            MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);


            Server.PrintToConsole($"MySQL {Config.DBHost} Connected");

            MySql.ExecuteNonQueryAsync(@"CREATE TABLE IF NOT EXISTS `users` (`id` INT AUTO_INCREMENT PRIMARY KEY, `steam_id` VARCHAR(32) UNIQUE NOT NULL, `end` INT(11) NOT NULL, UNIQUE (`steam_id`));");

        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"Error while loading: {ex.Message}");
        }
        RegisterListener<Listeners.OnMapStart>(name =>
        {
            Round = 0;
        });
        if (Config.DBUser.Length > 4)
        {

        }
        // Load VIP Prefix
        AddCommandListener("say", OnPlayerChat);
        AddCommandListener("say_team", OnPlayerChatTeam);
        // Load Commands

        // Load Smoke colors
        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
        RegisterListener<Listeners.OnTick>(() =>
        {
            for (int i = 1; i < Server.MaxPlayers; i++)
            {
                var ent = NativeAPI.GetEntityFromIndex(i);
                if (ent == 0)
                    continue;

                var client = new CCSPlayerController(ent);
                if (client == null || !client.IsValid)
                    continue;

                OnTick(client);
            }
        });


        if (hotReload)
        {
            RegisterListener<Listeners.OnMapStart>(name =>
            {
                ConnectedPlayers = 0;
                Round = 0;
            });

        }
    }

    public static void OnTick(CCSPlayerController controller)
    {
        if (!controller.PawnIsAlive)
            return;
        var pawn = controller.Pawn.Value;
        var flags = (PlayerFlags)pawn.Flags;
        var client = controller.EntityIndex.Value.Value;
        var buttons = controller.Buttons;

        if (IsVIP[client] == 0)
            return;

        if ((LF[client] & PlayerFlags.FL_ONGROUND) != 0 && (flags & PlayerFlags.FL_ONGROUND) == 0 &&
            (LB[client] & PlayerButtons.Jump) == 0 && (buttons & PlayerButtons.Jump) != 0)
        {
            J[client]++;
        }
        else if ((flags & PlayerFlags.FL_ONGROUND) != 0)
        {
            J[client] = 0;
        }
        else if ((LB[client] & PlayerButtons.Jump) == 0 && (buttons & PlayerButtons.Jump) != 0 && J[client] <= 1)
        {
            J[client]++;
            pawn.AbsVelocity.Z = 320;
        }

        LF[client] = flags;
        LB[client] = buttons;
    }
    internal static CCSGameRules GameRules()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
    }
    public void LoadPlayerData(CCSPlayerController player)
    {
        MySqlDb MySql = new MySqlDb(Config.DBHost, Config.DBUser, Config.DBPassword, Config.DBDatabase);

        MySqlQueryResult result = MySql!.Table("users").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select();
        if (result.Rows == 1)
        {
            var client = player.EntityIndex!.Value.Value;
            IsVIP[client] = 1;
            player.PrintToCenter("Congratulation! You have VIP");
            var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(result.Get<int>(0, "end")) - DateTimeOffset.UtcNow;
            var nowtimeis = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeRemainingFormatted =
            $"{timeRemaining.Days}d {timeRemaining.Hours}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
            Server.PrintToConsole($"VIP Plugin - Player {player.PlayerName} ({player.SteamID}) have VIP. Remaining time of VIP {timeRemainingFormatted}");
            // Checking if is still time to VIP
            if (result.Get<int>(0, "end") != 0)
            {
                if (result.Get<int>(0, "end") < nowtimeis)
                {
                    Server.PrintToConsole($"VIP Plugin - Player {player.PlayerName} ({player.SteamID}) exp. VIP today..");
                    MySql.Table("users").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Delete();
                    IsVIP[client] = 0;
                }
            }
            else
            {
                Server.PrintToConsole($"VIP Plugin - Player {player.PlayerName} ({player.SteamID}) have VIP forever");
            }
        }
        else
        {
            Server.PrintToConsole($"VIP Plugin - Player {player.PlayerName} ({player.SteamID}) is not VIP");
        }
    }
    private HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo info)
    {
        if (Config.EnableVIPPrefix)
        {
            var client = player.EntityIndex!.Value.Value;
            var message = info.GetArg(1);
            string message_first = info.GetArg(1);

            if (player == null || !player.IsValid || player.IsBot || message == null || message == "")
                return HookResult.Continue;
            if (message_first.Substring(0, 1) == "/" || message_first.Substring(0, 1) == "!" || message_first.Substring(0, 1) == "rtv")
                return HookResult.Continue;
            var GetTag = "";
            if (IsVIP[client] == 1)
            {
                GetTag = $" {ChatColors.Lime}VIP {ChatColors.Default}�";
            }

            var isAlive = player.PawnIsAlive ? "" : "-DEAD-";

            Server.PrintToChatAll(ReplaceTags($"{isAlive} {GetTag} {ChatColors.Red}{player.PlayerName} {ChatColors.Default}: {ChatColors.Lime}{message}"));
            return HookResult.Handled;
        }
        else
        {
            return HookResult.Continue;
        }
    }
    private HookResult OnPlayerChatTeam(CCSPlayerController? player, CommandInfo info)
    {
        if (Config.EnableVIPPrefix)
        {
            var client = player.EntityIndex!.Value.Value;
            var message = info.GetArg(1);
            string message_first = info.GetArg(1);

            if (player == null || !player.IsValid || player.IsBot || message == null || message == "")
                return HookResult.Continue;
            if (message_first.Substring(0, 1) == "/" || message_first.Substring(0, 1) == "!" || message_first.Substring(0, 1) == "rtv")
                return HookResult.Continue;
            var GetTag = "";
            if (IsVIP[client] == 1)
            {
                GetTag = $" {ChatColors.Lime}VIP {ChatColors.Default}�";
            }

            var isAlive = player.PawnIsAlive ? "" : "-DEAD-";
            for (int i = 1; i <= Server.MaxPlayers; i++)
            {
                CCSPlayerController? pc = Utilities.GetPlayerFromIndex(i);
                if (pc == null || !pc.IsValid || pc.IsBot || pc.TeamNum != player.TeamNum) continue;
                pc.PrintToChat(ReplaceTags($"{isAlive}(TEAM) {GetTag} {ChatColors.Red}{player.PlayerName} {ChatColors.Default}: {ChatColors.Lime}{message}"));
            }
            return HookResult.Handled;
        }
        else
        {
            return HookResult.Continue;
        }
    }
    private bool CheckIsHaveWeapon(string weapon_name, CCSPlayerController? pc)
    {
        foreach (var weapon in pc.PlayerPawn.Value.WeaponServices!.MyWeapons)
        {
            if (weapon is { IsValid: true, Value.IsValid: true })
            {
                if (weapon.Value.DesignerName.Contains($"{weapon_name}"))
                {
                    Server.PrintToConsole($"VIP Plugin - Requested weapon is weapon_{weapon_name}");
                    Server.PrintToConsole($"VIP Plugin - {pc.PlayerName} have weapon with name {weapon.Value.DesignerName}");
                    return true;
                }
            }
        }
        return false;
    }
    private void OnEntitySpawned(CEntityInstance entity)
    {
        if (!Config.EnableVIPColoredSmokes) return;
        if (entity.DesignerName != "smokegrenade_projectile") return;

        var smokeGrenadeEntity = new CSmokeGrenadeProjectile(entity.Handle);
        if (smokeGrenadeEntity.Handle == IntPtr.Zero) return;

        Server.NextFrame(() =>
        {
            var entityIndex = smokeGrenadeEntity.Thrower.Value.Controller.Value.EntityIndex!.Value.Value;

            if (entityIndex == null) return;
            if (IsVIP[entityIndex] == 0) return;

            smokeGrenadeEntity.SmokeColor.X = Random.Shared.NextSingle() * 255.0f;
            smokeGrenadeEntity.SmokeColor.Y = Random.Shared.NextSingle() * 255.0f;
            smokeGrenadeEntity.SmokeColor.Z = Random.Shared.NextSingle() * 255.0f;
        });
    }


    private string ReplaceTags(string message) // THX https://github.com/daffyyyy/CS2-Tags/blob/main/CS2-Tags.cs
    {
        if (message.Contains('{'))
        {
            string modifiedValue = message;
            foreach (FieldInfo field in typeof(ChatColors).GetFields())
            {
                string pattern = $"{{{field.Name}}}";
                if (message.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }
            return modifiedValue;
        }

        return message;
    }
    [GameEventHandler]
    public HookResult OnClientConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;


        var client = player.EntityIndex!.Value.Value;
        Used[client] = 0;
        LastUsed[client] = 0;
        IsVIP[client] = 0;
        ConnectedPlayers++;

        var slots = Server.MaxPlayers;
        slots = slots - Config.ReservedSlotsForVIP;
        if (ConnectedPlayers >= slots)
        {
            if (IsVIP[client] == 1)
            {
                Server.PrintToConsole($"VIP Plugins - Player {player.PlayerName} use the Reservated slot!");
                return HookResult.Continue;
            }
            else
            {
                Server.ExecuteCommand($"kickid {player.UserId} 'Server is full, this slot(s) is reserved for VIP!'");
                Server.PrintToConsole($"VIP Plugins - Player {player.PlayerName} is kicked from the server, bcs slot are for VIP!");
            }
        }


        LoadPlayerData(player);
        if (IsVIP[client] == 1)
        {
            IsVIP[client] = 1;
            if (Config.WelcomeMessageEnable)
            {
                player.PrintToChat($" {Config.WelcomeMessage}");
            }
        }

        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnClientDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        ConnectedPlayers--;
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (GameRules().WarmupPeriod)
        {
            Server.PrintToConsole("VIP Plugins - Warmup dosen't real Round, set on 0.");
            Round = 0;
        }
        else if (GameRules().OvertimePlaying == 1)
        {
            Server.PrintToConsole("VIP Plugins - Overtime dosen't real Round, set on 0.");
            Round = 0;
        }
        else if (GameRules().SwitchingTeamsAtRoundReset)
        {
            Server.PrintToConsole("VIP Plugins - Halftime/switch sites dosen't real Round, set on 0.");
            Round = 0;
        }
        else
        {
            Round++;
            Server.PrintToConsole($"VIP Plugins - Added new round count, now is {ConsoleColor.Yellow} {Round}.");
        }
        return HookResult.Continue;
    }
    private void Give_Values(CCSPlayerController controller)
    {
        if (controller == null || !controller.IsValid || controller.IsBot)
            return;

        var client = controller.EntityIndex!.Value.Value;
        var PawnValue = controller.PlayerPawn.Value;
        var moneyServices = controller.InGameMoneyServices;
        if (IsVIP[client] == 1)
        {
            if (Config.EnableVIPAcceries)
            {
                PawnValue.Health += 15;
                PawnValue.ArmorValue = 100;

                if (moneyServices.Account <= 800)
                {
                    moneyServices.Account = 1200;
                }
            }
        }

    }
    [GameEventHandler]
    public HookResult OnClientSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid;

        if (player.UserId != null)
        {
            var client = player.EntityIndex!.Value.Value;
            Used[client] = 0;
            Give_Values(player);
            if (LastUsed[client] == 1) {
                if (CheckIsHaveWeapon("ak47", player) == false)
                {
                    player.GiveNamedItem("weapon_ak47");
                }
                player.PrintToChat($" {Config.Prefix} You got automatically AK-47, if you wanna turn off type /guns_off.");
                Used[client] = 1;
            }
            else if (LastUsed[client] == 2)
            {

                // Weapons
                if (CheckIsHaveWeapon("deagle", player) == false)
                {
                    player.GiveNamedItem("weapon_deagle");
                }
                if (CheckIsHaveWeapon("ak47", player) == false)
                {
                    player.GiveNamedItem("weapon_ak47");
                }
                if (CheckIsHaveWeapon("healthshot", player) == false)
                {
                    player.GiveNamedItem("weapon_healthshot");

                }
                // Granades
                if (CheckIsHaveWeapon("molotov", player) == false)
                {
                    player.GiveNamedItem("weapon_molotov");
                }
                if (CheckIsHaveWeapon("smokegrenade", player) == false)
                {
                    player.GiveNamedItem("weapon_smokegrenade");
                }
                if (CheckIsHaveWeapon("hegrenade", player) == false)
                {
                    player.GiveNamedItem("weapon_hegrenade");
                }
                player.PrintToChat($" {Config.Prefix} You got automatically Pack 2, if you wanna turn off type /guns_off.");
                Used[client] = 1;
            }
            else if (LastUsed[client] == 3)
            {
                if (CheckIsHaveWeapon("deagle", player) == false)
                {
                    player.GiveNamedItem("weapon_deagle");
                }
                if (CheckIsHaveWeapon("m4a1", player) == false)
                {
                    player.GiveNamedItem("weapon_m4a1");
                }
                if (CheckIsHaveWeapon("healthshot", player) == false)
                {
                    player.GiveNamedItem("weapon_healthshot");

                }
                // Granades
                if (CheckIsHaveWeapon("molotov", player) == false)
                {
                    player.GiveNamedItem("weapon_molotov");
                }
                if (CheckIsHaveWeapon("smokegrenade", player) == false)
                {
                    player.GiveNamedItem("weapon_smokegrenade");
                }
                if (CheckIsHaveWeapon("hegrenade", player) == false)
                {
                    player.GiveNamedItem("weapon_hegrenade");
                }
                player.PrintToChat($" {Config.Prefix} You got automatically Pack 1, if you wanna turn off type /guns_off.");
                Used[client] = 1;
            }
            else if (LastUsed[client] == 4)
            {
                if (CheckIsHaveWeapon("m4a1", player) == false)
                {
                    player.GiveNamedItem("weapon_m4a1");
                }
                player.PrintToChat($" {Config.Prefix} You got automatically M4A1, if you wanna turn off type /guns_off.");
                Used[client] = 1;
            }
            else if (LastUsed[client] == 5)
            {
                if (CheckIsHaveWeapon("m4a1_silencer", player) == false)
                {
                    player.GiveNamedItem("weapon_m4a1_silencer");
                }
                player.PrintToChat($" {Config.Prefix} You got automatically M4A1, if you wanna turn off type /guns_off.");
                Used[client] = 1;
            }
            //player.PrintToChat($"{Config.Prefix} You can use /ak for give AK47 or /m4 for give M4A1");
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid;
        CCSPlayerController attacker = @event.Attacker;
        var PawnValueAttacker = attacker.PlayerPawn.Value;
        var MoneyValueAttacker = attacker.InGameMoneyServices;
        var attacker_entity = attacker.EntityIndex!.Value.Value;
        var player_entity = player.EntityIndex!.Value.Value;

        if (attacker.IsBot) return HookResult.Continue;
        if (player.Connected != PlayerConnectedState.PlayerConnected || !player.PlayerPawn.IsValid || !@event.Userid.IsValid) return HookResult.Continue;
        if (attacker.Connected != PlayerConnectedState.PlayerConnected || !attacker.PlayerPawn.IsValid || !@event.Attacker.IsValid || IsVIP[attacker_entity] == 0) return HookResult.Continue;

        if (Config.GiveHPAfterKill || Config.GiveMoneyAfterKill)
        {
            Server.PrintToChatAll($" {Config.Prefix} Player {ChatColors.Lime}{player.PlayerName}{ChatColors.Default} is killed by {ChatColors.Lime}{attacker.PlayerName}{ChatColors.Default}.");
        }
        if(Config.GiveHPAfterKill)
        {
            // Sometimes giving, sometimes no, Valve :)
            PawnValueAttacker.Health += 10;
            Server.PrintToConsole($"VIP Plugins - Here is bug from valve https://discord.com/channels/1160907911501991946/1160907912445710482/1175583981387927602");
            attacker.PrintToChat($" {Config.Prefix} You got {ChatColors.Lime}+10 HP{ChatColors.Default} for kill player {ChatColors.LightRed}{player.PlayerName}{ChatColors.Default}, enjoy.");
        }
        if(Config.GiveMoneyAfterKill)
        {
            var AttackerMoneys = MoneyValueAttacker.Account;
            MoneyValueAttacker.Account = AttackerMoneys + 300;
            attacker.PrintToChat($" {Config.Prefix} You got {ChatColors.Lime}+300 ${ChatColors.Default} for kill player {ChatColors.LightRed}{player.PlayerName}{ChatColors.Default}, enjoy.");

        }
        return HookResult.Continue;
    }
    [GameEventHandler(HookMode.Post)]
    
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid;
        CCSPlayerController attacker = @event.Attacker;

        var client = player.EntityIndex!.Value.Value;

        if (player.Connected != PlayerConnectedState.PlayerConnected || !player.PlayerPawn.IsValid || !@event.Userid.IsValid)
            return HookResult.Continue;
        if (IsVIP[client] == 0)
        {
            player.PrintToChat($" {Config.Prefix} {Config.TranslationClass.MustBeVIP}");
            return HookResult.Continue;
        }
        if (Config.KnifeDMGEnable)
        {
            if (@event.Weapon == "knife")
            {
                if (@event.Userid.PlayerPawn.Value.Health + @event.DmgHealth <= 100)
                {
                    @event.Userid.PlayerPawn.Value.Health = @event.Userid.PlayerPawn.Value.Health += @event.DmgHealth;
                    if(@event.Attacker.IsValid)
                    {
                        attacker.PrintToChat($" {Config.Prefix} You canno't hit {ChatColors.Lime}VIP {ChatColors.Default}player with Knife!");
                    }
                }
                else
                {
                    @event.Userid.PlayerPawn.Value.Health = 100;
                }
            }
        }
        @event.Userid.PlayerPawn.Value.VelocityModifier = 1;
        return HookResult.Continue;
    }
    // Database settings
}
