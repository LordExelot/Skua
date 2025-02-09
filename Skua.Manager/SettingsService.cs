﻿using Skua.Core.Interfaces;
using Skua.Manager.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skua.Manager;
public class SettingsService : ISettingsService
{
    public T? Get<T>(string key)
    {
        return (T?)Settings.Default[key];
    }

    public T Get<T>(string key, T defaultValue)
    {
        T value = (T)Settings.Default[key];
        return value is null ? defaultValue : value;
    }

    public void Set<T>(string key, T value)
    {
        Settings.Default[key] = value;
        Settings.Default.Save();
    }
}
