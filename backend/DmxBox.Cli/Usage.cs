namespace DmxBox.Cli;

/// <summary>The text for --help and for a command line that could not be understood.</summary>
internal static class Usage
{
    public const string Text = """
        Control a dmxbox.

        usage: dmxbox [options] <command> [arguments]

        commands:
          ping                              show version and state of the box
          discover                          list the Art-Net nodes on the network
          set <start> <value>...            snap channels start, start+1, ... to values
          fade <start> <value>...           fade to the values, computed on the box
          artnet <start> <value>...         send one raw Art-Net frame
          preset save|go|clear|show <slot>  store, recall, empty or print a preset
          master <level>                    set the master level
          blackout on|off                   switch blackout on or off
          release-artnet                    forget the last Art-Net look
          merge htp|override                how Art-Net and the cues are combined
          get [start] [count]               print the DMX output (default: 1 16)
          input [start] [count]             print what a console sends to DMX IN (dmxbox 4)
          demo cues|stream                  demos for 4 RGB PARs at channels 1-12

        options:
          -H, --host <host>         box address (default: DMXBOX_HOST or 192.168.7.1)
              --port <port>         control port (default: 7000)
              --artnet-port <port>  Art-Net port (default: 6454)
          -t, --time <seconds>      fade time of fade (default: 1) and preset go (0)
          -u, --universe <number>   universe of artnet (default: 0)
              --target <address>    target of discover (default: everybody, through every
                                    network adapter, so also the box on its USB cable)
          -h, --help                show this text

        Channels count from 1, presets from 0 to 15, values and levels from 0 to 255.
        """;
}
