﻿using Microsoft.Toolkit.Mvvm.ComponentModel;
using Skua.Core.Interfaces;
using Skua.Core.Models.Items;

namespace Skua.Core.Scripts;
public partial class ScriptBoost : ObservableObject, IScriptBoost, IAsyncDisposable
{
    private readonly Lazy<IScriptInventory> _lazyInventory;
    private readonly Lazy<IScriptBank> _lazyBank;
    private readonly Lazy<IScriptPlayer> _lazyPlayer;
    private readonly Lazy<IScriptSend> _lazySend;
    private readonly Lazy<IScriptWait> _lazyWait;
    private readonly Lazy<IScriptMap> _lazyMap;
    private readonly Lazy<IFlashUtil> _lazyFlash;
    private IScriptInventory Inventory => _lazyInventory.Value;
    private IScriptBank Bank => _lazyBank.Value;
    private IScriptSend Send => _lazySend.Value;
    private IScriptWait Wait => _lazyWait.Value;
    private IScriptMap Map => _lazyMap.Value;
    private IFlashUtil Flash => _lazyFlash.Value;
    public ScriptBoost(
        Lazy<IFlashUtil> flash,
        Lazy<IScriptSend> send,
        Lazy<IScriptMap> map,
        Lazy<IScriptInventory> inventory,
        Lazy<IScriptBank> bank,
        Lazy<IScriptPlayer> player,
        Lazy<IScriptWait> wait)
    {
        _lazyInventory = inventory;
        _lazyBank = bank;
        _lazyPlayer = player;
        _lazySend = send;
        _lazyWait = wait;
        _lazyMap = map;
        _lazyFlash = flash;
        _timerBoosts = new PeriodicTimer(TimeSpan.FromSeconds(30));
    }

    private PeriodicTimer _timerBoosts;
    private Task? _taskBoosts;
    private CancellationTokenSource? _ctsBoosts;

    public bool Enabled => _taskBoosts is not null;
    [ObservableProperty]
    private bool _UseClassBoost = false;
    [ObservableProperty]
    private int _ClassBoostID;
    [ObservableProperty]
    private bool _UseExperienceBoost = false;
    [ObservableProperty]
    private int _ExperienceBoostID;
    [ObservableProperty]
    private bool _UseGoldBoost = false;
    [ObservableProperty]
    private int _GoldBoostID;
    [ObservableProperty]
    private bool _UseReputationBoost = false;
    [ObservableProperty]
    private int _ReputationBoostID;

    public bool IsBoostActive(BoostType boost)
    {
        return Flash.GetGameObject($"world.myAvatar.objData.{_boostMap[boost]}", 0) > 0;
    }

    public void UseBoost(int id)
    {
        Send.Packet($"%xt%zm%serverUseItem%{Map.RoomID}%+%{id}%");
    }

    public int GetBoostID(BoostType boostType, bool searchBank = true)
    {
        return boostType switch
        {
            BoostType.Gold => SearchBoost("gold", searchBank),
            BoostType.Class => SearchBoost("class", searchBank),
            BoostType.Reputation => SearchBoost("rep", searchBank),
            BoostType.Experience => SearchBoost("xp", searchBank),
            _ => 0,
        };
    }

    private int SearchBoost(string name, bool searchBank = false)
    {
        if (!_lazyPlayer.Value.LoggedIn)
            return 0;
        int id = (Inventory.Items?
                   .Where(i => i.Category == ItemCategory.ServerUse)
                   .Where(i => i.Name.Contains(name))
                   .FirstOrDefault())?.ID ?? 0;
        if (id == 0 && searchBank)
        {
            if(!Bank.Loaded)
                Bank.Load();
            id = (Bank.Items?
                   .Where(i => i.Category == ItemCategory.ServerUse)
                   .Where(i => i.Name.Contains(name))
                   .FirstOrDefault())?.ID ?? 0;
            Bank.EnsureToInventory(id, false);
        }
        return id;
    }

    public void Start()
    {
        if (_taskBoosts is not null)
            return;

        _ctsBoosts = new();
        _taskBoosts = HandleBoosts(_timerBoosts, _ctsBoosts.Token);
        OnPropertyChanged(nameof(Enabled));
    }

    public void Stop()
    {
        if (_taskBoosts is null)
            return;

        _ctsBoosts?.Cancel();
        Wait.ForTrue(() => _taskBoosts?.IsCompleted == true, null, 20);
        _ctsBoosts?.Dispose();
        _taskBoosts = null;
        OnPropertyChanged(nameof(Enabled));
    }

    public async ValueTask StopAsync()
    {
        if (_taskBoosts is null)
            return;

        _ctsBoosts?.Cancel();
        await Wait.ForTrueAsync(() => _taskBoosts?.IsCompleted == true, 20);
        _ctsBoosts?.Dispose();
        _ctsBoosts = null;
        _taskBoosts = null;
        OnPropertyChanged(nameof(Enabled));
    }

    private async Task HandleBoosts(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            await PollBoosts(token);

            while (await timer.WaitForNextTickAsync(token))
                await PollBoosts(token);
        }
        catch { }
    }

    private async Task PollBoosts(CancellationToken token)
    {
        await _UseBoost(UseGoldBoost, GoldBoostID, BoostType.Gold, token);

        await _UseBoost(UseClassBoost, ClassBoostID, BoostType.Class, token);

        await _UseBoost(UseExperienceBoost, ExperienceBoostID, BoostType.Experience, token);

        await _UseBoost(UseReputationBoost, ReputationBoostID, BoostType.Reputation, token);
    }

    private async ValueTask _UseBoost(bool useBoost, int id, BoostType boostType, CancellationToken token)
    {
        if (!useBoost || id == 0 || IsBoostActive(boostType))
            return;

        UseBoost(id);
        await Task.Delay(1000, token);
    }

    public async ValueTask DisposeAsync()
    {
        _ctsBoosts?.Cancel();
        if(_taskBoosts is not null)
            await _taskBoosts;
        _timerBoosts.Dispose();
        _ctsBoosts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private readonly Dictionary<BoostType, string> _boostMap = new()
    {
        { BoostType.Gold, "iBoostG" },
        { BoostType.Class, "iBoostCP" },
        { BoostType.Reputation, "iBoostRep" },
        { BoostType.Experience, "iBoostXP" }
    };
}
