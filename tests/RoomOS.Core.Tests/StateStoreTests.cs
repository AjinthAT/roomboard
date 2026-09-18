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

    private static AudioState Audio(string? active = "jbl", int volume = 16) =>
        new(active, volume, false,
            [new AudioOutputInfo("jbl", "JBL", true), new AudioOutputInfo("headset", "Casque", true)],
            DateTimeOffset.UnixEpoch);

    /// <summary>
    /// L'agent republie l'état audio toutes les 10 s même sans changement. La
    /// comparaison doit porter sur le contenu : l'égalité de record compare la liste
    /// des sorties par référence, et chaque publication en porte une nouvelle.
    /// </summary>
    [Fact]
    public void Un_etat_audio_identique_n_est_pas_rediffuse()
    {
        var store = NewStore();
        var diffusions = 0;
        store.AudioStateChanged += _ => diffusions++;

        store.SetAudio("gaming-pc", Audio());
        store.SetAudio("gaming-pc", Audio());
        store.SetAudio("gaming-pc", Audio());

        Assert.Equal(1, diffusions);
    }

    [Fact]
    public void Un_changement_de_volume_est_diffuse()
    {
        var store = NewStore();
        store.SetAudio("gaming-pc", Audio(volume: 16));

        var diffusions = 0;
        store.AudioStateChanged += _ => diffusions++;
        store.SetAudio("gaming-pc", Audio(volume: 40));

        Assert.Equal(1, diffusions);
    }

    [Fact]
    public void Eteindre_le_pc_efface_la_sortie_active_et_debranche_tout()
    {
        var store = NewStore();
        store.SetOnline("gaming-pc", TimeSpan.FromSeconds(10));
        store.SetAudio("gaming-pc", Audio());

        store.SetOffline("gaming-pc");

        var audio = store.GetAudio("gaming-pc");
        Assert.NotNull(audio);
        Assert.Null(audio.ActiveOutputId);
        Assert.All(audio.Outputs, o => Assert.False(o.Connected));
    }
}
