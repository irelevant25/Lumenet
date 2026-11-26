using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for LAN: whether other devices in the network may use the desk, and its password.
internal partial class DeskHub
{
    /// <summary>
    /// Whether other devices in the network may use the desk. A UI on this PC also gets the
    /// addresses at which they reach it, and the password while LAN is on.
    /// </summary>
    public LanState GetLanAccess()
    {
        return _lan.StateFor(IsLocal);
    }

    /// <summary>
    /// Switches LAN on or off. Off cuts every other device off at once. Only a UI on this PC can
    /// do this.
    /// </summary>
    /// <param name="on">True to let devices in the network in, with the password.</param>
    public async Task SetLanAccess(bool on)
    {
        if (!IsLocal)
            throw new DeskException("LAN can only be switched on the PC.");

        _lan.Switch(on);
        await _lan.TellPcAsync();
    }

    /// <summary>
    /// Chooses the address of this PC at which devices in the network may use the desk, or lets
    /// the desk choose by itself again. Only a UI on this PC can do this.
    /// </summary>
    /// <param name="address">One of the PC's addresses in the network, like "192.168.1.20"; null or empty for "choose by yourself".</param>
    public async Task SetLanAddress(string? address)
    {
        if (!IsLocal)
            throw new DeskException("The LAN address can only be chosen on the PC.");

        _lan.Choose(address);
        await _lan.TellPcAsync();
    }

    /// <summary>
    /// Makes a typed text the LAN password. Every device has to give the new one; those that are
    /// connected lose their connection. Only a UI on this PC can do this.
    /// </summary>
    /// <param name="password">5 to 20 letters and digits.</param>
    public async Task SetLanPassword(string password)
    {
        if (!IsLocal)
            throw new DeskException("The LAN password can only be changed on the PC.");

        _lan.ChangePassword(password);
        await _lan.TellPcAsync();
    }

    /// <summary>Makes a new random LAN password, as <see cref="SetLanPassword"/> does with a typed one.</summary>
    public async Task NewLanPassword()
    {
        if (!IsLocal)
            throw new DeskException("The LAN password can only be changed on the PC.");

        _lan.RenewPassword();
        await _lan.TellPcAsync();
    }
}
