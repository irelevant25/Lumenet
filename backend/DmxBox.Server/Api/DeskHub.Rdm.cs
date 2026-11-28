namespace DmxBox.Server.Api;

// The hub's part for RDM: the lights with RDM on the DMX lines of the boxes. Each call
// waits for the boxes and the lights, so it runs off the hub's thread.
internal partial class DeskHub
{
    /// <summary>
    /// The boxes that answer, each with the IDs of the lights with RDM it found on its line, and
    /// whether it still searches (see <see cref="RdmService.Boxes"/>).
    /// </summary>
    public Task<RdmBoxView[]> GetRdmBoxes()
    {
        return Task.Run(_rdm.Boxes);
    }

    /// <summary>One light, read from the light itself: maker, model, name, address, mode and its modes.</summary>
    /// <param name="box">The box on whose line it is, as in <see cref="RdmBoxView.Address"/>.</param>
    /// <param name="id">The light's RDM ID, "7FF1:00000001".</param>
    public Task<RdmLightView> GetRdmLight(string box, string id)
    {
        return Task.Run(() => _rdm.Light(box, id));
    }

    /// <summary>
    /// Changes a light: its name, DMX address or mode, or whether it shows where it is. Answers the
    /// light as it is afterwards. The project does not change: no undo.
    /// </summary>
    /// <param name="box">The box on whose line it is.</param>
    /// <param name="id">The light's RDM ID.</param>
    /// <param name="change">What changes.</param>
    public Task<RdmLightView> SetRdmLight(string box, string id, RdmLightChange change)
    {
        return Task.Run(() => _rdm.Change(box, id, change));
    }

    /// <summary>The box searches its line for lights again; <see cref="GetRdmBoxes"/> says when it is done.</summary>
    /// <param name="box">The box, as in <see cref="RdmBoxView.Address"/>.</param>
    public Task SearchRdm(string box)
    {
        return Task.Run(() => _rdm.Search(box));
    }
}
