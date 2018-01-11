// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///     Represents a manage of objects that cretead on UWP.
/// </summary>
internal class UWPDefaultBridgeServiceManager : IUWPBridgeServiceManager
{
    private readonly List<IUWPBridgeService> _serviceCollection;
    private readonly object LockObject = new Object();

    internal UWPDefaultBridgeServiceManager()
    {
        lock (LockObject)
        {
            if (_serviceCollection == null)
                System.Diagnostics.Debug.WriteLine("constructor");
                _serviceCollection = new List<IUWPBridgeService>();
        }
    }

    /// <summary>
    ///     Adds an object of <see cref="IUWPBridgeService" />
    /// </summary>
    /// <typeparam name="T">Type of the object to be acquired</typeparam>
    /// <param name="service">instance of service</param>
    public void AddService<T>(IUWPBridgeService service) where T : IUWPBridgeService
    {
        System.Diagnostics.Debug.WriteLine("add_service");
        lock (LockObject)
        {
            _serviceCollection.Add(service);
        }
    }

    /// <summary>
    ///     Acquires the specified object from the registered objects.
    /// </summary>
    /// <typeparam name="T">Type of the object to be acquired</typeparam>
    /// <returns>object</returns>
    public T GetService<T>() where T : class, IUWPBridgeService
    {
        System.Diagnostics.Debug.WriteLine("get_service");
        var temp = _serviceCollection.FirstOrDefault(x => x is T) as T;
        System.Diagnostics.Debug.WriteLine(temp.ToString());
        return _serviceCollection.FirstOrDefault(x => x is T) as T;
    }
}