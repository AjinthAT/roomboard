using RoomOS.Core.State;
using RoomOS.Domain.Contracts;
using Xunit;

namespace RoomOS.Core.Tests;

/// <summary>
/// Régression : une télémétrie arrivant après la déconnexion laissait des mesures
/// mortes visibles dans le snapshot d'un PC hors ligne.
/// </summary>
public sealed class StateStoreTests
{
    private static readonly Telemetry Sample =
        new(12.2, 51, 1.0, 41.6, 2568, 16303, 25600, 65280);

    private static StateStore NewStore() => new(TimeProvider.System);

    [Fact]
    public void Une_telemetrie_arrivee_apres_la_deconnexion_est_ignoree()
    {
        var store = NewStore();
        store.SetOnline("gaming-pc", TimeSpan.FromSeconds(4021));
        store.SetOffline("gaming-pc");

        store.SetTelemetry("gaming-pc", Sample);

        var state = store.GetPc("gaming-pc");
        Assert.False(state.Online);
        Assert.Null(state.Telemetry);
    }

    [Fact]
    public void Une_telemetrie_recue_en_ligne_est_conservee()
    {
        var store = NewStore();
        store.SetOnline("gaming-pc", TimeSpan.FromSeconds(4021));

        store.SetTelemetry("gaming-pc", Sample);

        Assert.Equal(51, store.GetPc("gaming-pc").Telemetry?.CpuTempC);
    }

    [Fact]
    public void La_deconnexion_efface_la_derniere_telemetrie()
    {
        var store = NewStore();
        store.SetOnline("gaming-pc", TimeSpan.FromSeconds(4021));
        store.SetTelemetry("gaming-pc", Sample);

        store.SetOffline("gaming-pc");

        Assert.Null(store.GetPc("gaming-pc").Telemetry);
    }

    [Fact]
    public void Un_pc_jamais_vu_est_hors_ligne_sans_telemetrie()
    {
        var state = NewStore().GetPc("inconnu");

        Assert.False(state.Online);
        Assert.Null(state.Telemetry);
    }
}
