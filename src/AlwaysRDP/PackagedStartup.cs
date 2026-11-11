// Copyright (c) 2026 ryzyder. See LICENSE.
#if STORE_BUILD
using System;
using System.Threading;
using Windows.ApplicationModel;
using Windows.Foundation;

internal static class PackagedStartup {
    static T Wait<T>(IAsyncOperation<T> operation) {
        var deadline=DateTime.UtcNow.AddSeconds(5);
        while(operation.Status==AsyncStatus.Started) {
            if(DateTime.UtcNow>=deadline) {operation.Cancel();throw new TimeoutException(L.T("Windows no respondió al consultar el inicio automático."));}
            Thread.Sleep(10);
        }
        return operation.GetResults();
    }
    static StartupTask Task {get{return Wait(StartupTask.GetAsync("AlwaysRDPStartup"));}}
    internal static bool Enabled {get {var state=Task.State;return state==StartupTaskState.Enabled || state==StartupTaskState.EnabledByPolicy;}}
    internal static void Set(bool enabled) {
        var task=Task;
        if(!enabled){task.Disable();return;}
        var state=Wait(task.RequestEnableAsync());
        if(state!=StartupTaskState.Enabled && state!=StartupTaskState.EnabledByPolicy)
            throw new InvalidOperationException(L.T("Habilita AlwaysRDP en Configuración de Windows > Aplicaciones > Inicio. Windows mantiene el control de esta opción."));
    }
}
#endif
