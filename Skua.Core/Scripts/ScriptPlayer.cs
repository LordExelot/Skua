﻿using System.Drawing;
using Skua.Core.Interfaces;
using Skua.Core.Models.Monsters;
using Skua.Core.Models.Skills;
using Skua.Core.Models.Items;
using Skua.Core.Models.Players;
using Skua.Core.Flash;
using Skua.Core.Models;
using Microsoft.Toolkit.Mvvm.Messaging;
using Skua.Core.Messaging;

namespace Skua.Core.Scripts;
public partial class ScriptPlayer : IScriptPlayer
{
    private readonly Lazy<IFlashUtil> _lazyFlash;
    private readonly Lazy<IScriptEvent> _lazyEvents;
    private readonly Lazy<IScriptOption> _lazyOptions;
    private readonly Lazy<IScriptWait> _lazyWait;
    private readonly Lazy<IScriptInventory> _lazyInventory;
    private readonly IMessenger _messenger;
    private Lazy<string> _lazyUserName;
    private Lazy<string> _lazyPassword;
    private Lazy<(string UserName, string Password, string Guild)> _lazyLoginInfo;
    private IFlashUtil Flash => _lazyFlash.Value;
    private IScriptEvent Events => _lazyEvents.Value;
    private IScriptOption Options => _lazyOptions.Value;
    private IScriptInventory Inventory => _lazyInventory.Value;
    private IScriptWait Wait => _lazyWait.Value;

    public ScriptPlayer(
        Lazy<IFlashUtil> flash,
        Lazy<IScriptEvent> events,
        Lazy<IScriptOption> options,
        Lazy<IScriptWait> wait,
        Lazy<IScriptInventory> inventory,
        IMessenger messenger)
    {
        _lazyFlash = flash;
        _lazyEvents = events;
        _lazyOptions = options;
        _lazyWait = wait;
        _lazyInventory = inventory;
        _messenger = messenger;
        _lazyLoginInfo = new Lazy<(string UserName, string Password, string Guild)>(GetLoginInfo);

        _messenger.Register<ScriptPlayer, LogoutMessage>(this, ResetInfo);
    }

    [ObjectBinding("world.myAvatar.uid")]
    private int _ID;
    [ObjectBinding("world.myAvatar.objData.intExp")]
    private int _XP;
    [ObjectBinding("world.myAvatar.objData.intExpToLevel")]
    private int _requiredXP;
    [ObjectBinding("world.strFrame", Default = "string.Empty")]
    private string _cell;
    [ObjectBinding("world.strPad", Default = "string.Empty")]
    private string _pad;
    [ObjectBinding("serverIP", IsStatic = true, Default = "string.Empty")]
    private string _serverIP;
    public bool Playing => LoggedIn && Alive;
    [CallBinding("isLoggedIn")]
    private bool _loggedIn;
    //[ObjectBindingOld("loginInfo.strUsername", Static = true)]
    public string Username
    {
        get
        {
            if (!Playing)
                return string.Empty;

            return _lazyLoginInfo.Value.UserName;
        }
    }
    //[ObjectBindingOld("loginInfo.strPassword", Static = true)]
    public string Password
    {
        get
        {
            if (!Playing)
                return string.Empty;

            return _lazyLoginInfo.Value.Password;
        }
    }

    public string Guild
    {
        get
        {
            if (!Playing)
                return string.Empty;

            return _lazyLoginInfo.Value.Guild;
        }
    }
    [CallBinding("isKicked")]
    private bool _kicked;
    [ObjectBinding("world.myAvatar.dataLeaf.intState", RequireNotNull = "world.myAvatar")]
    private int _state;
    public bool InCombat => State == 2;
    public bool IsMember => Flash.GetGameObject<int>("world.myAvatar.objData.iUpgDays") >= 0;
    public bool Alive => State > 0;
    [ObjectBinding("world.myAvatar.dataLeaf.intHP")]
    private int _health;
    [ObjectBinding("world.myAvatar.dataLeaf.intHPMax")]
    private int _maxHealth;
    [ObjectBinding("world.myAvatar.objData.intMP")]
    private int _mana;
    [ObjectBinding("world.myAvatar.dataLeaf.intMPMax")]
    private int _maxMana;
    [ObjectBinding("world.myAvatar.dataLeaf.intLevel")]
    private int _level;
    [ObjectBinding("world.myAvatar.objData.intGold")]
    private int _gold;
    [ObjectBinding("world.myAvatar.objData.iRank")]
    private int _currentClassRank;
    public bool HasTarget
    {
        get
        {
            Monster? m = Target!;
            return m?.Alive ?? false;
        }
    }
    public bool Loaded => Flash.GetGameObject<int>("world.myAvatar.items.length") > 0
                        && !Flash.GetGameObject<bool>("world.mapLoadInProgress")
                        && Flash.CallGameFunction<bool>("world.myAvatar.pMC.artLoaded");
    [ObjectBinding("world.myAvatar.objData.intAccessLevel", HasSetter = true)]
    private int _accessLevel;
    public bool Upgrade
    {
        get
        {
            return Flash.GetGameObject<int>("world.myAvatar.objData.iUpgDays") > 0;
        }
        set
        {
            Flash.SetGameObject("world.myAvatar.objData.iUpg", value ? 1000 : 0);
            Flash.SetGameObject("world.myAvatar.objData.iUpgDays", value ? 1000 : 0);
        }
    }
    [ObjectBinding("world.actions.active", Default = "Array.Empty<Skua.Core.Models.Skills.SkillInfo>()")]
    private SkillInfo[] _skills;
    [ObjectBinding("world.myAvatar.dataLeaf.afk")]
    private bool _AFK;
    [ObjectBinding("world.myAvatar.pMC.x")]
    private int _X;
    [ObjectBinding("world.myAvatar.pMC.y")]
    private int _Y;
    [ObjectBinding("world.WALKSPEED", HasSetter = true, Default = "8")]
    private int _walkSpeed;
    [ObjectBinding("world.SCALE", HasSetter = true)]
    private int _scale;
    [ObjectBinding("world.myAvatar.target.objData", RequireNotNull = "world.myAvatar.target")]
    private Monster? _target;
    [ObjectBinding("world.myAvatar.dataLeaf.sta")]
    private PlayerStats? _stats;
    public InventoryItem? CurrentClass => Playing ? Inventory.Items?.Find(i => i.Equipped && i.Category == ItemCategory.Class) : null;

    public void Rest(bool full = false)
    {
        if (Options.SafeTimings)
            Wait.ForActionCooldown(GameActions.Rest);
        Flash.CallGameFunction("world.rest");
        if (full)
            Wait.ForTrue(() => Health >= MaxHealth && Mana >= MaxMana, 20);
    }

    [MethodCallBinding("walkTo", RunMethodPost = true)]
    private void _walkTo(int x, int y, int speed = 8)
    {
        if (Options.SafeTimings)
            Wait.ForPlayerPosition(x, y);
    }

    [MethodCallBinding("world.setSpawnPoint", GameFunction = true)]
    private void _setSpawnPoint(string cell, string pad) { }

    [MethodCallBinding("world.goto", GameFunction = true)]
    private void _goto(string name) { }

    private void ResetInfo(ScriptPlayer recipient, LogoutMessage message)
    {
        recipient._lazyLoginInfo = new(recipient.GetLoginInfo);
    }

    private (string UserName, string Password, string Guild) GetLoginInfo()
    {
        return (Flash.GetGameObjectStatic("loginInfo.strUsername", string.Empty)!, Flash.GetGameObjectStatic("loginInfo.strPassword", string.Empty)!, Flash.GetGameObject<string>("world.myAvatar.pMC.pname.tg.text")?.Replace("&lt; ", "< ").Replace(" &gt;", " >") ?? string.Empty);
    }
}