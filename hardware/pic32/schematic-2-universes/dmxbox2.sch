<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE eagle SYSTEM "eagle.dtd">
<eagle version="9.6.2">
<drawing>
<settings><setting alwaysvectorfont="no"/><setting verticaltext="up"/></settings>
<grid distance="0.1" unitdist="inch" unit="inch" style="lines" multiple="1" display="no" altdistance="0.01" altunitdist="inch" altunit="inch"/>
<layers>
<layer number="1" name="Top" color="4" fill="1" visible="yes" active="yes"/>
<layer number="16" name="Bottom" color="1" fill="1" visible="yes" active="yes"/>
<layer number="17" name="Pads" color="2" fill="1" visible="yes" active="yes"/>
<layer number="18" name="Vias" color="2" fill="1" visible="yes" active="yes"/>
<layer number="19" name="Unrouted" color="6" fill="1" visible="yes" active="yes"/>
<layer number="20" name="Dimension" color="15" fill="1" visible="yes" active="yes"/>
<layer number="21" name="tPlace" color="7" fill="1" visible="yes" active="yes"/>
<layer number="22" name="bPlace" color="7" fill="1" visible="yes" active="yes"/>
<layer number="25" name="tNames" color="7" fill="1" visible="yes" active="yes"/>
<layer number="26" name="bNames" color="7" fill="1" visible="yes" active="yes"/>
<layer number="27" name="tValues" color="7" fill="1" visible="yes" active="yes"/>
<layer number="28" name="bValues" color="7" fill="1" visible="yes" active="yes"/>
<layer number="29" name="tStop" color="7" fill="3" visible="yes" active="yes"/>
<layer number="30" name="bStop" color="7" fill="6" visible="yes" active="yes"/>
<layer number="35" name="tGlue" color="7" fill="4" visible="yes" active="yes"/>
<layer number="36" name="bGlue" color="7" fill="5" visible="yes" active="yes"/>
<layer number="39" name="tKeepout" color="4" fill="11" visible="yes" active="yes"/>
<layer number="40" name="bKeepout" color="1" fill="11" visible="yes" active="yes"/>
<layer number="41" name="tRestrict" color="4" fill="10" visible="yes" active="yes"/>
<layer number="42" name="bRestrict" color="1" fill="10" visible="yes" active="yes"/>
<layer number="43" name="vRestrict" color="2" fill="10" visible="yes" active="yes"/>
<layer number="44" name="Drills" color="7" fill="1" visible="yes" active="yes"/>
<layer number="45" name="Holes" color="7" fill="1" visible="yes" active="yes"/>
<layer number="46" name="Milling" color="3" fill="1" visible="yes" active="yes"/>
<layer number="47" name="Measures" color="7" fill="1" visible="yes" active="yes"/>
<layer number="48" name="Document" color="7" fill="1" visible="yes" active="yes"/>
<layer number="49" name="Reference" color="7" fill="1" visible="yes" active="yes"/>
<layer number="51" name="tDocu" color="7" fill="1" visible="yes" active="yes"/>
<layer number="52" name="bDocu" color="7" fill="1" visible="yes" active="yes"/>
<layer number="91" name="Nets" color="2" fill="1" visible="yes" active="yes"/>
<layer number="92" name="Busses" color="1" fill="1" visible="yes" active="yes"/>
<layer number="93" name="Pins" color="2" fill="1" visible="yes" active="yes"/>
<layer number="94" name="Symbols" color="4" fill="1" visible="yes" active="yes"/>
<layer number="95" name="Names" color="7" fill="1" visible="yes" active="yes"/>
<layer number="96" name="Values" color="7" fill="1" visible="yes" active="yes"/>
<layer number="97" name="Info" color="7" fill="1" visible="yes" active="yes"/>
<layer number="98" name="Guide" color="6" fill="1" visible="yes" active="yes"/>
</layers>
<schematic xreflabel="%F%N/%S.%C%R" xrefpart="/%S.%C%R">
<libraries>
<library name="lumenet">
<description>Parts of the Lumenet dmxbox, written by hardware/eagle.py.</description>
<packages>
<package name="C_5.08">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="5.08" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="6.58" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="6.58" y1="-1.5" x2="6.58" y2="1.5" width="0.2032" layer="21"/>
<wire x1="6.58" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="LED_3MM">
<pad name="A" x="0" y="0" drill="0.8" diameter="1.7" shape="square"/>
<pad name="K" x="2.54" y="0" drill="0.8" diameter="1.7" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="4.04" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="4.04" y1="-1.5" x2="4.04" y2="1.5" width="0.2032" layer="21"/>
<wire x1="4.04" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="DO15_12.70">
<pad name="1" x="0" y="0" drill="1.1" diameter="2" shape="square"/>
<pad name="2" x="12.7" y="0" drill="1.1" diameter="2" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="14.2" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="14.2" y1="-1.5" x2="14.2" y2="1.5" width="0.2032" layer="21"/>
<wire x1="14.2" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="USB_C_BREAKOUT_1X6">
<pad name="VBUS" x="0" y="0" drill="1" diameter="1.9" shape="square"/>
<pad name="D-" x="0" y="-2.54" drill="1" diameter="1.9" shape="round"/>
<pad name="D+" x="0" y="-5.08" drill="1" diameter="1.9" shape="round"/>
<pad name="CC1" x="0" y="-7.62" drill="1" diameter="1.9" shape="round"/>
<pad name="CC2" x="0" y="-10.16" drill="1" diameter="1.9" shape="round"/>
<pad name="GND" x="0" y="-12.7" drill="1" diameter="1.9" shape="round"/>
<wire x1="-1.5" y1="-14.2" x2="1.5" y2="-14.2" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-14.2" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-14.2" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-16.1" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="HEADER_1X6">
<pad name="1" x="0" y="0" drill="1" diameter="1.9" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="1" diameter="1.9" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="1" diameter="1.9" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="1" diameter="1.9" shape="round"/>
<pad name="5" x="0" y="-10.16" drill="1" diameter="1.9" shape="round"/>
<pad name="6" x="0" y="-12.7" drill="1" diameter="1.9" shape="round"/>
<wire x1="-1.5" y1="-14.2" x2="1.5" y2="-14.2" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-14.2" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-14.2" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-16.1" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="TERMINAL_3_5.08">
<pad name="1" x="0" y="0" drill="1.3" diameter="2.2" shape="square"/>
<pad name="2" x="0" y="-10.16" drill="1.3" diameter="2.2" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="1.3" diameter="2.2" shape="round"/>
<wire x1="-1.5" y1="-11.66" x2="1.5" y2="-11.66" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-11.66" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-11.66" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-13.56" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="R_10.16">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="10.16" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="11.66" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="11.66" y1="-1.5" x2="11.66" y2="1.5" width="0.2032" layer="21"/>
<wire x1="11.66" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="R_5.08_STANDING">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="5.08" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="6.58" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="6.58" y1="-1.5" x2="6.58" y2="1.5" width="0.2032" layer="21"/>
<wire x1="6.58" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="R_7.62">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="7.62" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="9.12" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="9.12" y1="-1.5" x2="9.12" y2="1.5" width="0.2032" layer="21"/>
<wire x1="9.12" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="PIANO_DIP_SWITCH_8">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="0.9" diameter="1.8" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="0.9" diameter="1.8" shape="round"/>
<pad name="5" x="0" y="-10.16" drill="0.9" diameter="1.8" shape="round"/>
<pad name="6" x="0" y="-12.7" drill="0.9" diameter="1.8" shape="round"/>
<pad name="7" x="0" y="-15.24" drill="0.9" diameter="1.8" shape="round"/>
<pad name="8" x="0" y="-17.78" drill="0.9" diameter="1.8" shape="round"/>
<pad name="9" x="7.62" y="-17.78" drill="0.9" diameter="1.8" shape="round"/>
<pad name="10" x="7.62" y="-15.24" drill="0.9" diameter="1.8" shape="round"/>
<pad name="11" x="7.62" y="-12.7" drill="0.9" diameter="1.8" shape="round"/>
<pad name="12" x="7.62" y="-10.16" drill="0.9" diameter="1.8" shape="round"/>
<pad name="13" x="7.62" y="-7.62" drill="0.9" diameter="1.8" shape="round"/>
<pad name="14" x="7.62" y="-5.08" drill="0.9" diameter="1.8" shape="round"/>
<pad name="15" x="7.62" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="16" x="7.62" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.59" y1="2.045" x2="-1.59" y2="-19.825" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="-19.825" x2="9.21" y2="-19.825" width="0.2032" layer="21"/>
<wire x1="9.21" y1="-19.825" x2="9.21" y2="2.045" width="0.2032" layer="21"/>
<wire x1="9.21" y1="2.045" x2="-1.59" y2="2.045" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="0.7" x2="-3.36" y2="0.7" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="0.7" x2="-3.36" y2="-0.7" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-0.7" x2="-1.59" y2="-0.7" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="-1.84" x2="-3.36" y2="-1.84" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-1.84" x2="-3.36" y2="-3.24" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-3.24" x2="-1.59" y2="-3.24" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="-4.38" x2="-3.36" y2="-4.38" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-4.38" x2="-3.36" y2="-5.78" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-5.78" x2="-1.59" y2="-5.78" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="-6.92" x2="-3.36" y2="-6.92" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-6.92" x2="-3.36" y2="-8.32" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-8.32" x2="-1.59" y2="-8.32" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="-9.46" x2="-3.36" y2="-9.46" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-9.46" x2="-3.36" y2="-10.86" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-10.86" x2="-1.59" y2="-10.86" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="-12" x2="-3.36" y2="-12" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-12" x2="-3.36" y2="-13.4" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-13.4" x2="-1.59" y2="-13.4" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="-14.54" x2="-3.36" y2="-14.54" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-14.54" x2="-3.36" y2="-15.94" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-15.94" x2="-1.59" y2="-15.94" width="0.2032" layer="21"/>
<wire x1="-1.59" y1="-17.08" x2="-3.36" y2="-17.08" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-17.08" x2="-3.36" y2="-18.48" width="0.2032" layer="21"/>
<wire x1="-3.36" y1="-18.48" x2="-1.59" y2="-18.48" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-21.18" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="BUTTON_12MM">
<pad name="a" x="0" y="0" drill="1.1" diameter="2" shape="square"/>
<pad name="net" x="0" y="-12.7" drill="1.1" diameter="2" shape="round"/>
<pad name="gnd" x="-5.08" y="0" drill="1.1" diameter="2" shape="round"/>
<pad name="b" x="-5.08" y="-12.7" drill="1.1" diameter="2" shape="round"/>
<wire x1="-6.58" y1="-14.2" x2="1.5" y2="-14.2" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-14.2" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-6.58" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-6.58" y1="1.5" x2="-6.58" y2="-14.2" width="0.2032" layer="21"/>
<text x="-6.58" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-6.58" y="-16.1" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="PIC32MX270F256B_DIP28">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="0.9" diameter="1.8" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="0.9" diameter="1.8" shape="round"/>
<pad name="5" x="0" y="-10.16" drill="0.9" diameter="1.8" shape="round"/>
<pad name="6" x="0" y="-12.7" drill="0.9" diameter="1.8" shape="round"/>
<pad name="7" x="0" y="-15.24" drill="0.9" diameter="1.8" shape="round"/>
<pad name="8" x="0" y="-17.78" drill="0.9" diameter="1.8" shape="round"/>
<pad name="9" x="0" y="-20.32" drill="0.9" diameter="1.8" shape="round"/>
<pad name="10" x="0" y="-22.86" drill="0.9" diameter="1.8" shape="round"/>
<pad name="11" x="0" y="-25.4" drill="0.9" diameter="1.8" shape="round"/>
<pad name="12" x="0" y="-27.94" drill="0.9" diameter="1.8" shape="round"/>
<pad name="13" x="0" y="-30.48" drill="0.9" diameter="1.8" shape="round"/>
<pad name="14" x="0" y="-33.02" drill="0.9" diameter="1.8" shape="round"/>
<pad name="15" x="7.62" y="-33.02" drill="0.9" diameter="1.8" shape="round"/>
<pad name="16" x="7.62" y="-30.48" drill="0.9" diameter="1.8" shape="round"/>
<pad name="17" x="7.62" y="-27.94" drill="0.9" diameter="1.8" shape="round"/>
<pad name="18" x="7.62" y="-25.4" drill="0.9" diameter="1.8" shape="round"/>
<pad name="19" x="7.62" y="-22.86" drill="0.9" diameter="1.8" shape="round"/>
<pad name="20" x="7.62" y="-20.32" drill="0.9" diameter="1.8" shape="round"/>
<pad name="21" x="7.62" y="-17.78" drill="0.9" diameter="1.8" shape="round"/>
<pad name="22" x="7.62" y="-15.24" drill="0.9" diameter="1.8" shape="round"/>
<pad name="23" x="7.62" y="-12.7" drill="0.9" diameter="1.8" shape="round"/>
<pad name="24" x="7.62" y="-10.16" drill="0.9" diameter="1.8" shape="round"/>
<pad name="25" x="7.62" y="-7.62" drill="0.9" diameter="1.8" shape="round"/>
<pad name="26" x="7.62" y="-5.08" drill="0.9" diameter="1.8" shape="round"/>
<pad name="27" x="7.62" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="28" x="7.62" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-34.52" x2="9.12" y2="-34.52" width="0.2032" layer="21"/>
<wire x1="9.12" y1="-34.52" x2="9.12" y2="1.5" width="0.2032" layer="21"/>
<wire x1="9.12" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-34.52" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-36.42" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="MCP1700_TO92">
<pad name="1" x="0" y="0" drill="0.8" diameter="1.7" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.8" diameter="1.7" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="0.8" diameter="1.7" shape="round"/>
<wire x1="-1.5" y1="-6.58" x2="1.5" y2="-6.58" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-6.58" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-6.58" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-8.48" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="RKK0505SH_SIP7">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="5" x="0" y="-10.16" drill="0.9" diameter="1.8" shape="round"/>
<pad name="7" x="0" y="-15.24" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-16.74" x2="1.5" y2="-16.74" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-16.74" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-16.74" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-18.64" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="6N137_DIP8">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="0.9" diameter="1.8" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="0.9" diameter="1.8" shape="round"/>
<pad name="5" x="7.62" y="-7.62" drill="0.9" diameter="1.8" shape="round"/>
<pad name="6" x="7.62" y="-5.08" drill="0.9" diameter="1.8" shape="round"/>
<pad name="7" x="7.62" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="8" x="7.62" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-9.12" x2="9.12" y2="-9.12" width="0.2032" layer="21"/>
<wire x1="9.12" y1="-9.12" x2="9.12" y2="1.5" width="0.2032" layer="21"/>
<wire x1="9.12" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-9.12" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-11.02" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="PC817_DIP4">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="3" x="7.62" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="4" x="7.62" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-4.04" x2="9.12" y2="-4.04" width="0.2032" layer="21"/>
<wire x1="9.12" y1="-4.04" x2="9.12" y2="1.5" width="0.2032" layer="21"/>
<wire x1="9.12" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-4.04" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-5.94" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="MAX485_DIP8">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.8" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="0.9" diameter="1.8" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="0.9" diameter="1.8" shape="round"/>
<pad name="5" x="7.62" y="-7.62" drill="0.9" diameter="1.8" shape="round"/>
<pad name="6" x="7.62" y="-5.08" drill="0.9" diameter="1.8" shape="round"/>
<pad name="7" x="7.62" y="-2.54" drill="0.9" diameter="1.8" shape="round"/>
<pad name="8" x="7.62" y="0" drill="0.9" diameter="1.8" shape="round"/>
<wire x1="-1.5" y1="-9.12" x2="9.12" y2="-9.12" width="0.2032" layer="21"/>
<wire x1="9.12" y1="-9.12" x2="9.12" y2="1.5" width="0.2032" layer="21"/>
<wire x1="9.12" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-9.12" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-11.02" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="HC49_CRYSTAL">
<pad name="1" x="0" y="0" drill="0.8" diameter="1.7" shape="square"/>
<pad name="2" x="5.08" y="0" drill="0.8" diameter="1.7" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="6.58" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="6.58" y1="-1.5" x2="6.58" y2="1.5" width="0.2032" layer="21"/>
<wire x1="6.58" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
</packages>
<symbols>
<symbol name="C_5.08">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="1" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="2" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="LED_3MM">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="A" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="K" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="DO15_12.70">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="1" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="2" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="USB_C_BREAKOUT_1X6">
<wire x1="-5.08" y1="-7.62" x2="5.08" y2="-7.62" width="0.254" layer="94"/>
<wire x1="5.08" y1="-7.62" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-7.62" width="0.254" layer="94"/>
<pin name="VBUS" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="D-" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="D+" x="-7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="CC1" x="7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="CC2" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="GND" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-10.42" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="HEADER_1X6">
<wire x1="-5.08" y1="-7.62" x2="5.08" y2="-7.62" width="0.254" layer="94"/>
<wire x1="5.08" y1="-7.62" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-7.62" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="-7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="4" x="7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="5" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="6" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-10.42" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="TERMINAL_3_5.08">
<wire x1="-5.08" y1="-5.08" x2="5.08" y2="-5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="-5.08" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-5.08" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-7.88" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="R_10.16">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="1" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="2" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="R_5.08_STANDING">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="1" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="2" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="R_7.62">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="1" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="2" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="PIANO_DIP_SWITCH_8">
<wire x1="-5.08" y1="-20.32" x2="5.08" y2="-20.32" width="0.254" layer="94"/>
<wire x1="5.08" y1="-20.32" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-20.32" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="-7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="4" x="-7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="5" x="-7.62" y="-10.16" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="6" x="-7.62" y="-12.7" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="7" x="-7.62" y="-15.24" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="8" x="-7.62" y="-17.78" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="9" x="7.62" y="-17.78" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="10" x="7.62" y="-15.24" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="11" x="7.62" y="-12.7" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="12" x="7.62" y="-10.16" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="13" x="7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="14" x="7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="15" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="16" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-23.12" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="BUTTON_12MM">
<wire x1="-5.08" y1="-5.08" x2="5.08" y2="-5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="-5.08" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-5.08" width="0.254" layer="94"/>
<pin name="a" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="net" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="gnd" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="b" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-7.88" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="PIC32MX270F256B_DIP28">
<wire x1="-7.62" y1="-35.56" x2="7.62" y2="-35.56" width="0.254" layer="94"/>
<wire x1="7.62" y1="-35.56" x2="7.62" y2="2.54" width="0.254" layer="94"/>
<wire x1="7.62" y1="2.54" x2="-7.62" y2="2.54" width="0.254" layer="94"/>
<wire x1="-7.62" y1="2.54" x2="-7.62" y2="-35.56" width="0.254" layer="94"/>
<pin name="MCLR@1" x="-10.16" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RA0@2" x="-10.16" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RA1@3" x="-10.16" y="-5.08" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RB0/PGED1@4" x="-10.16" y="-7.62" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RB1/PGEC1@5" x="-10.16" y="-10.16" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RB2@6" x="-10.16" y="-12.7" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RB3@7" x="-10.16" y="-15.24" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="VSS@8" x="-10.16" y="-17.78" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="OSC1@9" x="-10.16" y="-20.32" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="OSC2@10" x="-10.16" y="-22.86" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RB4@11" x="-10.16" y="-25.4" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RA4@12" x="-10.16" y="-27.94" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="VDD@13" x="-10.16" y="-30.48" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="RB5@14" x="-10.16" y="-33.02" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="VBUS@15" x="10.16" y="-33.02" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="RB7@16" x="10.16" y="-30.48" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="RB8/U2RX@17" x="10.16" y="-27.94" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="RB9/U2TX@18" x="10.16" y="-25.4" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="VSS@19" x="10.16" y="-22.86" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="VCAP@20" x="10.16" y="-20.32" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="D+@21" x="10.16" y="-17.78" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="D-@22" x="10.16" y="-15.24" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="VUSB3V3@23" x="10.16" y="-12.7" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="RB13/U1RX@24" x="10.16" y="-10.16" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="RB14@25" x="10.16" y="-7.62" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="RB15/U1TX@26" x="10.16" y="-5.08" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="AVSS@27" x="10.16" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="AVDD@28" x="10.16" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-7.62" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-7.62" y="-38.36" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="MCP1700_TO92">
<wire x1="-5.08" y1="-5.08" x2="5.08" y2="-5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="-5.08" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-5.08" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-7.88" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="RKK0505SH_SIP7">
<wire x1="-5.08" y1="-5.08" x2="5.08" y2="-5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="-5.08" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-5.08" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="5" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="7" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-7.88" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="6N137_DIP8">
<wire x1="-5.08" y1="-10.16" x2="5.08" y2="-10.16" width="0.254" layer="94"/>
<wire x1="5.08" y1="-10.16" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-10.16" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="-7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="4" x="-7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="5" x="7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="6" x="7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="7" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="8" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-12.96" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="PC817_DIP4">
<wire x1="-5.08" y1="-5.08" x2="5.08" y2="-5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="-5.08" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-5.08" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="4" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-7.88" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="MAX485_DIP8">
<wire x1="-5.08" y1="-10.16" x2="5.08" y2="-10.16" width="0.254" layer="94"/>
<wire x1="5.08" y1="-10.16" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-10.16" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="-7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="4" x="-7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="5" x="7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="6" x="7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="7" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="8" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-12.96" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="HC49_CRYSTAL">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="1" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="2" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
</symbols>
<devicesets>
<deviceset name="C_5.08" prefix="C"><gates><gate name="G$1" symbol="C_5.08" x="0" y="0"/></gates><devices><device name="" package="C_5.08"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="LED_3MM" prefix="D"><gates><gate name="G$1" symbol="LED_3MM" x="0" y="0"/></gates><devices><device name="" package="LED_3MM"><connects><connect gate="G$1" pin="A" pad="A"/><connect gate="G$1" pin="K" pad="K"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="DO15_12.70" prefix="D"><gates><gate name="G$1" symbol="DO15_12.70" x="0" y="0"/></gates><devices><device name="" package="DO15_12.70"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="USB_C_BREAKOUT_1X6" prefix="J"><gates><gate name="G$1" symbol="USB_C_BREAKOUT_1X6" x="0" y="0"/></gates><devices><device name="" package="USB_C_BREAKOUT_1X6"><connects><connect gate="G$1" pin="VBUS" pad="VBUS"/><connect gate="G$1" pin="D-" pad="D-"/><connect gate="G$1" pin="D+" pad="D+"/><connect gate="G$1" pin="CC1" pad="CC1"/><connect gate="G$1" pin="CC2" pad="CC2"/><connect gate="G$1" pin="GND" pad="GND"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="HEADER_1X6" prefix="J"><gates><gate name="G$1" symbol="HEADER_1X6" x="0" y="0"/></gates><devices><device name="" package="HEADER_1X6"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="6" pad="6"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="TERMINAL_3_5.08" prefix="J"><gates><gate name="G$1" symbol="TERMINAL_3_5.08" x="0" y="0"/></gates><devices><device name="" package="TERMINAL_3_5.08"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="R_10.16" prefix="R"><gates><gate name="G$1" symbol="R_10.16" x="0" y="0"/></gates><devices><device name="" package="R_10.16"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="R_5.08_STANDING" prefix="R"><gates><gate name="G$1" symbol="R_5.08_STANDING" x="0" y="0"/></gates><devices><device name="" package="R_5.08_STANDING"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="R_7.62" prefix="R"><gates><gate name="G$1" symbol="R_7.62" x="0" y="0"/></gates><devices><device name="" package="R_7.62"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="PIANO_DIP_SWITCH_8" prefix="SW"><gates><gate name="G$1" symbol="PIANO_DIP_SWITCH_8" x="0" y="0"/></gates><devices><device name="" package="PIANO_DIP_SWITCH_8"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="6" pad="6"/><connect gate="G$1" pin="7" pad="7"/><connect gate="G$1" pin="8" pad="8"/><connect gate="G$1" pin="9" pad="9"/><connect gate="G$1" pin="10" pad="10"/><connect gate="G$1" pin="11" pad="11"/><connect gate="G$1" pin="12" pad="12"/><connect gate="G$1" pin="13" pad="13"/><connect gate="G$1" pin="14" pad="14"/><connect gate="G$1" pin="15" pad="15"/><connect gate="G$1" pin="16" pad="16"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="BUTTON_12MM" prefix="SW"><gates><gate name="G$1" symbol="BUTTON_12MM" x="0" y="0"/></gates><devices><device name="" package="BUTTON_12MM"><connects><connect gate="G$1" pin="a" pad="a"/><connect gate="G$1" pin="net" pad="net"/><connect gate="G$1" pin="gnd" pad="gnd"/><connect gate="G$1" pin="b" pad="b"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="PIC32MX270F256B_DIP28" prefix="U"><gates><gate name="G$1" symbol="PIC32MX270F256B_DIP28" x="0" y="0"/></gates><devices><device name="" package="PIC32MX270F256B_DIP28"><connects><connect gate="G$1" pin="MCLR@1" pad="1"/><connect gate="G$1" pin="RA0@2" pad="2"/><connect gate="G$1" pin="RA1@3" pad="3"/><connect gate="G$1" pin="RB0/PGED1@4" pad="4"/><connect gate="G$1" pin="RB1/PGEC1@5" pad="5"/><connect gate="G$1" pin="RB2@6" pad="6"/><connect gate="G$1" pin="RB3@7" pad="7"/><connect gate="G$1" pin="VSS@8" pad="8"/><connect gate="G$1" pin="OSC1@9" pad="9"/><connect gate="G$1" pin="OSC2@10" pad="10"/><connect gate="G$1" pin="RB4@11" pad="11"/><connect gate="G$1" pin="RA4@12" pad="12"/><connect gate="G$1" pin="VDD@13" pad="13"/><connect gate="G$1" pin="RB5@14" pad="14"/><connect gate="G$1" pin="VBUS@15" pad="15"/><connect gate="G$1" pin="RB7@16" pad="16"/><connect gate="G$1" pin="RB8/U2RX@17" pad="17"/><connect gate="G$1" pin="RB9/U2TX@18" pad="18"/><connect gate="G$1" pin="VSS@19" pad="19"/><connect gate="G$1" pin="VCAP@20" pad="20"/><connect gate="G$1" pin="D+@21" pad="21"/><connect gate="G$1" pin="D-@22" pad="22"/><connect gate="G$1" pin="VUSB3V3@23" pad="23"/><connect gate="G$1" pin="RB13/U1RX@24" pad="24"/><connect gate="G$1" pin="RB14@25" pad="25"/><connect gate="G$1" pin="RB15/U1TX@26" pad="26"/><connect gate="G$1" pin="AVSS@27" pad="27"/><connect gate="G$1" pin="AVDD@28" pad="28"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="MCP1700_TO92" prefix="U"><gates><gate name="G$1" symbol="MCP1700_TO92" x="0" y="0"/></gates><devices><device name="" package="MCP1700_TO92"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="RKK0505SH_SIP7" prefix="U"><gates><gate name="G$1" symbol="RKK0505SH_SIP7" x="0" y="0"/></gates><devices><device name="" package="RKK0505SH_SIP7"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="7" pad="7"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="6N137_DIP8" prefix="U"><gates><gate name="G$1" symbol="6N137_DIP8" x="0" y="0"/></gates><devices><device name="" package="6N137_DIP8"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="6" pad="6"/><connect gate="G$1" pin="7" pad="7"/><connect gate="G$1" pin="8" pad="8"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="PC817_DIP4" prefix="U"><gates><gate name="G$1" symbol="PC817_DIP4" x="0" y="0"/></gates><devices><device name="" package="PC817_DIP4"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="MAX485_DIP8" prefix="U"><gates><gate name="G$1" symbol="MAX485_DIP8" x="0" y="0"/></gates><devices><device name="" package="MAX485_DIP8"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="6" pad="6"/><connect gate="G$1" pin="7" pad="7"/><connect gate="G$1" pin="8" pad="8"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="HC49_CRYSTAL" prefix="Y"><gates><gate name="G$1" symbol="HC49_CRYSTAL" x="0" y="0"/></gates><devices><device name="" package="HC49_CRYSTAL"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
</devicesets>
</library>
</libraries>
<attributes/>
<variantdefs/>
<classes><class number="0" name="default" width="0" drill="0"/></classes>
<parts>
<part name="C1" library="lumenet" deviceset="C_5.08" device="" value="10 µF"/>
<part name="C2" library="lumenet" deviceset="C_5.08" device="" value="10 µF"/>
<part name="C3" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C4" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C5" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C6" library="lumenet" deviceset="C_5.08" device="" value="10 µF"/>
<part name="C7" library="lumenet" deviceset="C_5.08" device="" value="22 pF"/>
<part name="C8" library="lumenet" deviceset="C_5.08" device="" value="22 pF"/>
<part name="C9" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C10" library="lumenet" deviceset="C_5.08" device="" value="10 µF"/>
<part name="C11" library="lumenet" deviceset="C_5.08" device="" value="10 µF"/>
<part name="C12" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C13" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C14" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C15" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C16" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="C17" library="lumenet" deviceset="C_5.08" device="" value="100 nF"/>
<part name="D1" library="lumenet" deviceset="LED_3MM" device="" value="LED 3 mm"/>
<part name="D2" library="lumenet" deviceset="DO15_12.70" device="" value="P6KE6.8CA"/>
<part name="D3" library="lumenet" deviceset="DO15_12.70" device="" value="P6KE6.8CA"/>
<part name="D4" library="lumenet" deviceset="DO15_12.70" device="" value="P6KE6.8CA"/>
<part name="D5" library="lumenet" deviceset="DO15_12.70" device="" value="P6KE6.8CA"/>
<part name="J1" library="lumenet" deviceset="USB_C_BREAKOUT_1X6" device="" value="USB 2.0 Type-C breakout"/>
<part name="J2" library="lumenet" deviceset="HEADER_1X6" device="" value="pin header 1x6"/>
<part name="J3" library="lumenet" deviceset="TERMINAL_3_5.08" device="" value="XLR 3-pin female, panel"/>
<part name="J4" library="lumenet" deviceset="TERMINAL_3_5.08" device="" value="XLR 3-pin female, panel"/>
<part name="R1" library="lumenet" deviceset="R_10.16" device="" value="5.1 kΩ"/>
<part name="R2" library="lumenet" deviceset="R_10.16" device="" value="5.1 kΩ"/>
<part name="R3" library="lumenet" deviceset="R_10.16" device="" value="10 kΩ"/>
<part name="R4" library="lumenet" deviceset="R_5.08_STANDING" device="" value="1 kΩ"/>
<part name="R5" library="lumenet" deviceset="R_10.16" device="" value="470 Ω"/>
<part name="R6" library="lumenet" deviceset="R_10.16" device="" value="180 Ω"/>
<part name="R7" library="lumenet" deviceset="R_5.08_STANDING" device="" value="1 kΩ"/>
<part name="R8" library="lumenet" deviceset="R_10.16" device="" value="390 Ω"/>
<part name="R9" library="lumenet" deviceset="R_10.16" device="" value="1 kΩ"/>
<part name="R10" library="lumenet" deviceset="R_7.62" device="" value="470 Ω"/>
<part name="R11" library="lumenet" deviceset="R_10.16" device="" value="10 kΩ"/>
<part name="R12" library="lumenet" deviceset="R_10.16" device="" value="560 Ω"/>
<part name="R13" library="lumenet" deviceset="R_10.16" device="" value="130 Ω"/>
<part name="R14" library="lumenet" deviceset="R_10.16" device="" value="560 Ω"/>
<part name="R15" library="lumenet" deviceset="R_10.16" device="" value="180 Ω"/>
<part name="R16" library="lumenet" deviceset="R_5.08_STANDING" device="" value="1 kΩ"/>
<part name="R17" library="lumenet" deviceset="R_10.16" device="" value="390 Ω"/>
<part name="R18" library="lumenet" deviceset="R_7.62" device="" value="1 kΩ"/>
<part name="R19" library="lumenet" deviceset="R_7.62" device="" value="470 Ω"/>
<part name="R20" library="lumenet" deviceset="R_10.16" device="" value="10 kΩ"/>
<part name="R21" library="lumenet" deviceset="R_10.16" device="" value="560 Ω"/>
<part name="R22" library="lumenet" deviceset="R_10.16" device="" value="130 Ω"/>
<part name="R23" library="lumenet" deviceset="R_10.16" device="" value="560 Ω"/>
<part name="SW1" library="lumenet" deviceset="PIANO_DIP_SWITCH_8" device="" value="piano DIP switch, 8 positions (DPL08RGT)"/>
<part name="SW2" library="lumenet" deviceset="BUTTON_12MM" device="" value="tact switch 12 x 12 mm"/>
<part name="SW3" library="lumenet" deviceset="BUTTON_12MM" device="" value="tact switch 12 x 12 mm"/>
<part name="U1" library="lumenet" deviceset="PIC32MX270F256B_DIP28" device="" value="PIC32MX270F256B-I/SP"/>
<part name="U2" library="lumenet" deviceset="MCP1700_TO92" device="" value="MCP1700-3302E/TO"/>
<part name="U3" library="lumenet" deviceset="RKK0505SH_SIP7" device="" value="RKK-0505S/H"/>
<part name="U4" library="lumenet" deviceset="6N137_DIP8" device="" value="6N137"/>
<part name="U5" library="lumenet" deviceset="6N137_DIP8" device="" value="6N137"/>
<part name="U6" library="lumenet" deviceset="PC817_DIP4" device="" value="PC817"/>
<part name="U7" library="lumenet" deviceset="MAX485_DIP8" device="" value="MAX485"/>
<part name="U8" library="lumenet" deviceset="6N137_DIP8" device="" value="6N137"/>
<part name="U9" library="lumenet" deviceset="6N137_DIP8" device="" value="6N137"/>
<part name="U10" library="lumenet" deviceset="PC817_DIP4" device="" value="PC817"/>
<part name="U11" library="lumenet" deviceset="MAX485_DIP8" device="" value="MAX485"/>
<part name="Y1" library="lumenet" deviceset="HC49_CRYSTAL" device="" value="8 MHz"/>
</parts>
<sheets>
<sheet>
<plain><text x="0" y="25.4" size="2.54" layer="97">dmxbox 2: USB to DMX512</text><text x="0" y="20.32" size="1.778" layer="97">Written by hardware/pic32/schematic-2-universes/eagle_export.py from circuit.py and perfboard.py.</text></plain>
<instances>
<instance part="C1" gate="G$1" x="0" y="0"/>
<instance part="C2" gate="G$1" x="45.72" y="0"/>
<instance part="C3" gate="G$1" x="91.44" y="0"/>
<instance part="C4" gate="G$1" x="137.16" y="0"/>
<instance part="C5" gate="G$1" x="182.88" y="0"/>
<instance part="C6" gate="G$1" x="228.6" y="0"/>
<instance part="C7" gate="G$1" x="0" y="-15.24"/>
<instance part="C8" gate="G$1" x="45.72" y="-15.24"/>
<instance part="C9" gate="G$1" x="91.44" y="-15.24"/>
<instance part="C10" gate="G$1" x="137.16" y="-15.24"/>
<instance part="C11" gate="G$1" x="182.88" y="-15.24"/>
<instance part="C12" gate="G$1" x="228.6" y="-15.24"/>
<instance part="C13" gate="G$1" x="0" y="-30.48"/>
<instance part="C14" gate="G$1" x="45.72" y="-30.48"/>
<instance part="C15" gate="G$1" x="91.44" y="-30.48"/>
<instance part="C16" gate="G$1" x="137.16" y="-30.48"/>
<instance part="C17" gate="G$1" x="182.88" y="-30.48"/>
<instance part="D1" gate="G$1" x="228.6" y="-30.48"/>
<instance part="D2" gate="G$1" x="0" y="-45.72"/>
<instance part="D3" gate="G$1" x="45.72" y="-45.72"/>
<instance part="D4" gate="G$1" x="91.44" y="-45.72"/>
<instance part="D5" gate="G$1" x="137.16" y="-45.72"/>
<instance part="J1" gate="G$1" x="182.88" y="-45.72"/>
<instance part="J2" gate="G$1" x="233.68" y="-45.72"/>
<instance part="J3" gate="G$1" x="0" y="-66.04"/>
<instance part="J4" gate="G$1" x="50.8" y="-66.04"/>
<instance part="R1" gate="G$1" x="101.6" y="-66.04"/>
<instance part="R2" gate="G$1" x="147.32" y="-66.04"/>
<instance part="R3" gate="G$1" x="193.04" y="-66.04"/>
<instance part="R4" gate="G$1" x="238.76" y="-66.04"/>
<instance part="R5" gate="G$1" x="0" y="-83.82"/>
<instance part="R6" gate="G$1" x="45.72" y="-83.82"/>
<instance part="R7" gate="G$1" x="91.44" y="-83.82"/>
<instance part="R8" gate="G$1" x="137.16" y="-83.82"/>
<instance part="R9" gate="G$1" x="182.88" y="-83.82"/>
<instance part="R10" gate="G$1" x="228.6" y="-83.82"/>
<instance part="R11" gate="G$1" x="0" y="-99.06"/>
<instance part="R12" gate="G$1" x="45.72" y="-99.06"/>
<instance part="R13" gate="G$1" x="91.44" y="-99.06"/>
<instance part="R14" gate="G$1" x="137.16" y="-99.06"/>
<instance part="R15" gate="G$1" x="182.88" y="-99.06"/>
<instance part="R16" gate="G$1" x="228.6" y="-99.06"/>
<instance part="R17" gate="G$1" x="0" y="-114.3"/>
<instance part="R18" gate="G$1" x="45.72" y="-114.3"/>
<instance part="R19" gate="G$1" x="91.44" y="-114.3"/>
<instance part="R20" gate="G$1" x="137.16" y="-114.3"/>
<instance part="R21" gate="G$1" x="182.88" y="-114.3"/>
<instance part="R22" gate="G$1" x="228.6" y="-114.3"/>
<instance part="R23" gate="G$1" x="0" y="-129.54"/>
<instance part="SW1" gate="G$1" x="45.72" y="-129.54"/>
<instance part="SW2" gate="G$1" x="96.52" y="-129.54"/>
<instance part="SW3" gate="G$1" x="147.32" y="-129.54"/>
<instance part="U1" gate="G$1" x="198.12" y="-129.54"/>
<instance part="U2" gate="G$1" x="254" y="-129.54"/>
<instance part="U3" gate="G$1" x="0" y="-177.8"/>
<instance part="U4" gate="G$1" x="50.8" y="-177.8"/>
<instance part="U5" gate="G$1" x="101.6" y="-177.8"/>
<instance part="U6" gate="G$1" x="152.4" y="-177.8"/>
<instance part="U7" gate="G$1" x="203.2" y="-177.8"/>
<instance part="U8" gate="G$1" x="254" y="-177.8"/>
<instance part="U9" gate="G$1" x="0" y="-200.66"/>
<instance part="U10" gate="G$1" x="50.8" y="-200.66"/>
<instance part="U11" gate="G$1" x="101.6" y="-200.66"/>
<instance part="Y1" gate="G$1" x="152.4" y="-200.66"/>
</instances>
<busses/>
<nets>
<net name="+5V" class="0"><segment><pinref part="J1" gate="G$1" pin="VBUS"/><wire x1="175.26" y1="-45.72" x2="170.18" y2="-45.72" width="0.1524" layer="91"/><label x="164.88" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="C1" gate="G$1" pin="1"/><wire x1="-5.08" y1="0" x2="-10.16" y2="0" width="0.1524" layer="91"/><label x="-15.46" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U2" gate="G$1" pin="2"/><wire x1="246.38" y1="-132.08" x2="241.3" y2="-132.08" width="0.1524" layer="91"/><label x="236" y="-131.68" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="VBUS@15"/><wire x1="208.28" y1="-162.56" x2="213.36" y2="-162.56" width="0.1524" layer="91"/><label x="213.86" y="-162.16" size="1.27" layer="95"/></segment><segment><pinref part="U3" gate="G$1" pin="1"/><wire x1="-7.62" y1="-177.8" x2="-12.7" y2="-177.8" width="0.1524" layer="91"/><label x="-18" y="-177.4" size="1.27" layer="95"/></segment><segment><pinref part="C10" gate="G$1" pin="1"/><wire x1="132.08" y1="-15.24" x2="127" y2="-15.24" width="0.1524" layer="91"/><label x="121.7" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="8"/><wire x1="109.22" y1="-177.8" x2="114.3" y2="-177.8" width="0.1524" layer="91"/><label x="114.8" y="-177.4" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="7"/><wire x1="109.22" y1="-180.34" x2="114.3" y2="-180.34" width="0.1524" layer="91"/><label x="114.8" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="C13" gate="G$1" pin="1"/><wire x1="-5.08" y1="-30.48" x2="-10.16" y2="-30.48" width="0.1524" layer="91"/><label x="-15.46" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="U9" gate="G$1" pin="8"/><wire x1="7.62" y1="-200.66" x2="12.7" y2="-200.66" width="0.1524" layer="91"/><label x="13.2" y="-200.26" size="1.27" layer="95"/></segment><segment><pinref part="U9" gate="G$1" pin="7"/><wire x1="7.62" y1="-203.2" x2="12.7" y2="-203.2" width="0.1524" layer="91"/><label x="13.2" y="-202.8" size="1.27" layer="95"/></segment><segment><pinref part="C16" gate="G$1" pin="1"/><wire x1="132.08" y1="-30.48" x2="127" y2="-30.48" width="0.1524" layer="91"/><label x="121.7" y="-30.08" size="1.27" layer="95"/></segment></net>
<net name="GND" class="0"><segment><pinref part="J1" gate="G$1" pin="GND"/><wire x1="190.5" y1="-45.72" x2="195.58" y2="-45.72" width="0.1524" layer="91"/><label x="196.08" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="R1" gate="G$1" pin="2"/><wire x1="106.68" y1="-66.04" x2="111.76" y2="-66.04" width="0.1524" layer="91"/><label x="112.26" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="R2" gate="G$1" pin="2"/><wire x1="152.4" y1="-66.04" x2="157.48" y2="-66.04" width="0.1524" layer="91"/><label x="157.98" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="C1" gate="G$1" pin="2"/><wire x1="5.08" y1="0" x2="10.16" y2="0" width="0.1524" layer="91"/><label x="10.66" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U2" gate="G$1" pin="1"/><wire x1="246.38" y1="-129.54" x2="241.3" y2="-129.54" width="0.1524" layer="91"/><label x="236" y="-129.14" size="1.27" layer="95"/></segment><segment><pinref part="C2" gate="G$1" pin="2"/><wire x1="50.8" y1="0" x2="55.88" y2="0" width="0.1524" layer="91"/><label x="56.38" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="VSS@8"/><wire x1="187.96" y1="-147.32" x2="182.88" y2="-147.32" width="0.1524" layer="91"/><label x="177.58" y="-146.92" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="VSS@19"/><wire x1="208.28" y1="-152.4" x2="213.36" y2="-152.4" width="0.1524" layer="91"/><label x="213.86" y="-152" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="AVSS@27"/><wire x1="208.28" y1="-132.08" x2="213.36" y2="-132.08" width="0.1524" layer="91"/><label x="213.86" y="-131.68" size="1.27" layer="95"/></segment><segment><pinref part="C3" gate="G$1" pin="2"/><wire x1="96.52" y1="0" x2="101.6" y2="0" width="0.1524" layer="91"/><label x="102.1" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="C4" gate="G$1" pin="2"/><wire x1="142.24" y1="0" x2="147.32" y2="0" width="0.1524" layer="91"/><label x="147.82" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="C5" gate="G$1" pin="2"/><wire x1="187.96" y1="0" x2="193.04" y2="0" width="0.1524" layer="91"/><label x="193.54" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="C6" gate="G$1" pin="2"/><wire x1="233.68" y1="0" x2="238.76" y2="0" width="0.1524" layer="91"/><label x="239.26" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="C7" gate="G$1" pin="2"/><wire x1="5.08" y1="-15.24" x2="10.16" y2="-15.24" width="0.1524" layer="91"/><label x="10.66" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="C8" gate="G$1" pin="2"/><wire x1="50.8" y1="-15.24" x2="55.88" y2="-15.24" width="0.1524" layer="91"/><label x="56.38" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="C9" gate="G$1" pin="2"/><wire x1="96.52" y1="-15.24" x2="101.6" y2="-15.24" width="0.1524" layer="91"/><label x="102.1" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="J2" gate="G$1" pin="3"/><wire x1="226.06" y1="-50.8" x2="220.98" y2="-50.8" width="0.1524" layer="91"/><label x="215.68" y="-50.4" size="1.27" layer="95"/></segment><segment><pinref part="U3" gate="G$1" pin="2"/><wire x1="-7.62" y1="-180.34" x2="-12.7" y2="-180.34" width="0.1524" layer="91"/><label x="-18" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="C10" gate="G$1" pin="2"/><wire x1="142.24" y1="-15.24" x2="147.32" y2="-15.24" width="0.1524" layer="91"/><label x="147.82" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="D1" gate="G$1" pin="K"/><wire x1="233.68" y1="-30.48" x2="238.76" y2="-30.48" width="0.1524" layer="91"/><label x="239.26" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="1"/><wire x1="38.1" y1="-129.54" x2="33.02" y2="-129.54" width="0.1524" layer="91"/><label x="27.72" y="-129.14" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="2"/><wire x1="38.1" y1="-132.08" x2="33.02" y2="-132.08" width="0.1524" layer="91"/><label x="27.72" y="-131.68" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="3"/><wire x1="38.1" y1="-134.62" x2="33.02" y2="-134.62" width="0.1524" layer="91"/><label x="27.72" y="-134.22" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="4"/><wire x1="38.1" y1="-137.16" x2="33.02" y2="-137.16" width="0.1524" layer="91"/><label x="27.72" y="-136.76" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="5"/><wire x1="38.1" y1="-139.7" x2="33.02" y2="-139.7" width="0.1524" layer="91"/><label x="27.72" y="-139.3" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="6"/><wire x1="38.1" y1="-142.24" x2="33.02" y2="-142.24" width="0.1524" layer="91"/><label x="27.72" y="-141.84" size="1.27" layer="95"/></segment><segment><pinref part="U6" gate="G$1" pin="2"/><wire x1="144.78" y1="-180.34" x2="139.7" y2="-180.34" width="0.1524" layer="91"/><label x="134.4" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="5"/><wire x1="109.22" y1="-185.42" x2="114.3" y2="-185.42" width="0.1524" layer="91"/><label x="114.8" y="-185.02" size="1.27" layer="95"/></segment><segment><pinref part="C13" gate="G$1" pin="2"/><wire x1="5.08" y1="-30.48" x2="10.16" y2="-30.48" width="0.1524" layer="91"/><label x="10.66" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="U10" gate="G$1" pin="2"/><wire x1="43.18" y1="-203.2" x2="38.1" y2="-203.2" width="0.1524" layer="91"/><label x="32.8" y="-202.8" size="1.27" layer="95"/></segment><segment><pinref part="U9" gate="G$1" pin="5"/><wire x1="7.62" y1="-208.28" x2="12.7" y2="-208.28" width="0.1524" layer="91"/><label x="13.2" y="-207.88" size="1.27" layer="95"/></segment><segment><pinref part="C16" gate="G$1" pin="2"/><wire x1="142.24" y1="-30.48" x2="147.32" y2="-30.48" width="0.1524" layer="91"/><label x="147.82" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="SW2" gate="G$1" pin="gnd"/><wire x1="104.14" y1="-132.08" x2="109.22" y2="-132.08" width="0.1524" layer="91"/><label x="109.72" y="-131.68" size="1.27" layer="95"/></segment><segment><pinref part="SW3" gate="G$1" pin="gnd"/><wire x1="154.94" y1="-132.08" x2="160.02" y2="-132.08" width="0.1524" layer="91"/><label x="160.52" y="-131.68" size="1.27" layer="95"/></segment></net>
<net name="+3V3" class="0"><segment><pinref part="U2" gate="G$1" pin="3"/><wire x1="261.62" y1="-129.54" x2="266.7" y2="-129.54" width="0.1524" layer="91"/><label x="267.2" y="-129.14" size="1.27" layer="95"/></segment><segment><pinref part="C2" gate="G$1" pin="1"/><wire x1="40.64" y1="0" x2="35.56" y2="0" width="0.1524" layer="91"/><label x="28.66" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="VDD@13"/><wire x1="187.96" y1="-160.02" x2="182.88" y2="-160.02" width="0.1524" layer="91"/><label x="175.98" y="-159.62" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="VUSB3V3@23"/><wire x1="208.28" y1="-142.24" x2="213.36" y2="-142.24" width="0.1524" layer="91"/><label x="213.86" y="-141.84" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="AVDD@28"/><wire x1="208.28" y1="-129.54" x2="213.36" y2="-129.54" width="0.1524" layer="91"/><label x="213.86" y="-129.14" size="1.27" layer="95"/></segment><segment><pinref part="C3" gate="G$1" pin="1"/><wire x1="86.36" y1="0" x2="81.28" y2="0" width="0.1524" layer="91"/><label x="74.38" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="C5" gate="G$1" pin="1"/><wire x1="177.8" y1="0" x2="172.72" y2="0" width="0.1524" layer="91"/><label x="165.82" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="C4" gate="G$1" pin="1"/><wire x1="132.08" y1="0" x2="127" y2="0" width="0.1524" layer="91"/><label x="120.1" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="R3" gate="G$1" pin="1"/><wire x1="187.96" y1="-66.04" x2="182.88" y2="-66.04" width="0.1524" layer="91"/><label x="175.98" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="J2" gate="G$1" pin="2"/><wire x1="226.06" y1="-48.26" x2="220.98" y2="-48.26" width="0.1524" layer="91"/><label x="214.08" y="-47.86" size="1.27" layer="95"/></segment><segment><pinref part="R6" gate="G$1" pin="1"/><wire x1="40.64" y1="-83.82" x2="35.56" y2="-83.82" width="0.1524" layer="91"/><label x="28.66" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="R9" gate="G$1" pin="1"/><wire x1="177.8" y1="-83.82" x2="172.72" y2="-83.82" width="0.1524" layer="91"/><label x="165.82" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="R15" gate="G$1" pin="1"/><wire x1="177.8" y1="-99.06" x2="172.72" y2="-99.06" width="0.1524" layer="91"/><label x="165.82" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="R18" gate="G$1" pin="1"/><wire x1="40.64" y1="-114.3" x2="35.56" y2="-114.3" width="0.1524" layer="91"/><label x="28.66" y="-113.9" size="1.27" layer="95"/></segment></net>
<net name="CC1" class="0"><segment><pinref part="J1" gate="G$1" pin="CC1"/><wire x1="190.5" y1="-50.8" x2="195.58" y2="-50.8" width="0.1524" layer="91"/><label x="196.08" y="-50.4" size="1.27" layer="95"/></segment><segment><pinref part="R1" gate="G$1" pin="1"/><wire x1="96.52" y1="-66.04" x2="91.44" y2="-66.04" width="0.1524" layer="91"/><label x="86.14" y="-65.64" size="1.27" layer="95"/></segment></net>
<net name="CC2" class="0"><segment><pinref part="J1" gate="G$1" pin="CC2"/><wire x1="190.5" y1="-48.26" x2="195.58" y2="-48.26" width="0.1524" layer="91"/><label x="196.08" y="-47.86" size="1.27" layer="95"/></segment><segment><pinref part="R2" gate="G$1" pin="1"/><wire x1="142.24" y1="-66.04" x2="137.16" y2="-66.04" width="0.1524" layer="91"/><label x="131.86" y="-65.64" size="1.27" layer="95"/></segment></net>
<net name="D+" class="0"><segment><pinref part="J1" gate="G$1" pin="D+"/><wire x1="175.26" y1="-50.8" x2="170.18" y2="-50.8" width="0.1524" layer="91"/><label x="166.48" y="-50.4" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="D+@21"/><wire x1="208.28" y1="-147.32" x2="213.36" y2="-147.32" width="0.1524" layer="91"/><label x="213.86" y="-146.92" size="1.27" layer="95"/></segment></net>
<net name="D-" class="0"><segment><pinref part="J1" gate="G$1" pin="D-"/><wire x1="175.26" y1="-48.26" x2="170.18" y2="-48.26" width="0.1524" layer="91"/><label x="166.48" y="-47.86" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="D-@22"/><wire x1="208.28" y1="-144.78" x2="213.36" y2="-144.78" width="0.1524" layer="91"/><label x="213.86" y="-144.38" size="1.27" layer="95"/></segment></net>
<net name="VCAP" class="0"><segment><pinref part="U1" gate="G$1" pin="VCAP@20"/><wire x1="208.28" y1="-149.86" x2="213.36" y2="-149.86" width="0.1524" layer="91"/><label x="213.86" y="-149.46" size="1.27" layer="95"/></segment><segment><pinref part="C6" gate="G$1" pin="1"/><wire x1="223.52" y1="0" x2="218.44" y2="0" width="0.1524" layer="91"/><label x="211.54" y="0.4" size="1.27" layer="95"/></segment></net>
<net name="OSC1" class="0"><segment><pinref part="U1" gate="G$1" pin="OSC1@9"/><wire x1="187.96" y1="-149.86" x2="182.88" y2="-149.86" width="0.1524" layer="91"/><label x="175.98" y="-149.46" size="1.27" layer="95"/></segment><segment><pinref part="Y1" gate="G$1" pin="1"/><wire x1="147.32" y1="-200.66" x2="142.24" y2="-200.66" width="0.1524" layer="91"/><label x="135.34" y="-200.26" size="1.27" layer="95"/></segment><segment><pinref part="C7" gate="G$1" pin="1"/><wire x1="-5.08" y1="-15.24" x2="-10.16" y2="-15.24" width="0.1524" layer="91"/><label x="-17.06" y="-14.84" size="1.27" layer="95"/></segment></net>
<net name="OSC2" class="0"><segment><pinref part="U1" gate="G$1" pin="OSC2@10"/><wire x1="187.96" y1="-152.4" x2="182.88" y2="-152.4" width="0.1524" layer="91"/><label x="175.98" y="-152" size="1.27" layer="95"/></segment><segment><pinref part="Y1" gate="G$1" pin="2"/><wire x1="157.48" y1="-200.66" x2="162.56" y2="-200.66" width="0.1524" layer="91"/><label x="163.06" y="-200.26" size="1.27" layer="95"/></segment><segment><pinref part="C8" gate="G$1" pin="1"/><wire x1="40.64" y1="-15.24" x2="35.56" y2="-15.24" width="0.1524" layer="91"/><label x="28.66" y="-14.84" size="1.27" layer="95"/></segment></net>
<net name="MCLR_RC" class="0"><segment><pinref part="R3" gate="G$1" pin="2"/><wire x1="198.12" y1="-66.04" x2="203.2" y2="-66.04" width="0.1524" layer="91"/><label x="203.7" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="R4" gate="G$1" pin="1"/><wire x1="233.68" y1="-66.04" x2="228.6" y2="-66.04" width="0.1524" layer="91"/><label x="216.9" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="C9" gate="G$1" pin="1"/><wire x1="86.36" y1="-15.24" x2="81.28" y2="-15.24" width="0.1524" layer="91"/><label x="69.58" y="-14.84" size="1.27" layer="95"/></segment></net>
<net name="MCLR" class="0"><segment><pinref part="R4" gate="G$1" pin="2"/><wire x1="243.84" y1="-66.04" x2="248.92" y2="-66.04" width="0.1524" layer="91"/><label x="249.42" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="MCLR@1"/><wire x1="187.96" y1="-129.54" x2="182.88" y2="-129.54" width="0.1524" layer="91"/><label x="175.98" y="-129.14" size="1.27" layer="95"/></segment><segment><pinref part="J2" gate="G$1" pin="1"/><wire x1="226.06" y1="-45.72" x2="220.98" y2="-45.72" width="0.1524" layer="91"/><label x="214.08" y="-45.32" size="1.27" layer="95"/></segment></net>
<net name="BLACKOUT" class="0"><segment><pinref part="U1" gate="G$1" pin="RB0/PGED1@4"/><wire x1="187.96" y1="-137.16" x2="182.88" y2="-137.16" width="0.1524" layer="91"/><label x="169.58" y="-136.76" size="1.27" layer="95"/></segment><segment><pinref part="J2" gate="G$1" pin="4"/><wire x1="241.3" y1="-50.8" x2="246.38" y2="-50.8" width="0.1524" layer="91"/><label x="246.88" y="-50.4" size="1.27" layer="95"/></segment><segment><pinref part="SW2" gate="G$1" pin="net"/><wire x1="88.9" y1="-132.08" x2="83.82" y2="-132.08" width="0.1524" layer="91"/><label x="70.52" y="-131.68" size="1.27" layer="95"/></segment></net>
<net name="PRESET" class="0"><segment><pinref part="U1" gate="G$1" pin="RB1/PGEC1@5"/><wire x1="187.96" y1="-139.7" x2="182.88" y2="-139.7" width="0.1524" layer="91"/><label x="172.78" y="-139.3" size="1.27" layer="95"/></segment><segment><pinref part="J2" gate="G$1" pin="5"/><wire x1="241.3" y1="-48.26" x2="246.38" y2="-48.26" width="0.1524" layer="91"/><label x="246.88" y="-47.86" size="1.27" layer="95"/></segment><segment><pinref part="SW3" gate="G$1" pin="net"/><wire x1="139.7" y1="-132.08" x2="134.62" y2="-132.08" width="0.1524" layer="91"/><label x="124.52" y="-131.68" size="1.27" layer="95"/></segment></net>
<net name="N1" class="0"><segment><pinref part="U1" gate="G$1" pin="RA0@2"/><wire x1="187.96" y1="-132.08" x2="182.88" y2="-132.08" width="0.1524" layer="91"/><label x="179.18" y="-131.68" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="16"/><wire x1="53.34" y1="-129.54" x2="58.42" y2="-129.54" width="0.1524" layer="91"/><label x="58.92" y="-129.14" size="1.27" layer="95"/></segment></net>
<net name="N2" class="0"><segment><pinref part="U1" gate="G$1" pin="RA1@3"/><wire x1="187.96" y1="-134.62" x2="182.88" y2="-134.62" width="0.1524" layer="91"/><label x="179.18" y="-134.22" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="15"/><wire x1="53.34" y1="-132.08" x2="58.42" y2="-132.08" width="0.1524" layer="91"/><label x="58.92" y="-131.68" size="1.27" layer="95"/></segment></net>
<net name="N4" class="0"><segment><pinref part="U1" gate="G$1" pin="RB2@6"/><wire x1="187.96" y1="-142.24" x2="182.88" y2="-142.24" width="0.1524" layer="91"/><label x="179.18" y="-141.84" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="14"/><wire x1="53.34" y1="-134.62" x2="58.42" y2="-134.62" width="0.1524" layer="91"/><label x="58.92" y="-134.22" size="1.27" layer="95"/></segment></net>
<net name="N8" class="0"><segment><pinref part="U1" gate="G$1" pin="RB3@7"/><wire x1="187.96" y1="-144.78" x2="182.88" y2="-144.78" width="0.1524" layer="91"/><label x="179.18" y="-144.38" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="13"/><wire x1="53.34" y1="-137.16" x2="58.42" y2="-137.16" width="0.1524" layer="91"/><label x="58.92" y="-136.76" size="1.27" layer="95"/></segment></net>
<net name="N16" class="0"><segment><pinref part="U1" gate="G$1" pin="RB4@11"/><wire x1="187.96" y1="-154.94" x2="182.88" y2="-154.94" width="0.1524" layer="91"/><label x="177.58" y="-154.54" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="12"/><wire x1="53.34" y1="-139.7" x2="58.42" y2="-139.7" width="0.1524" layer="91"/><label x="58.92" y="-139.3" size="1.27" layer="95"/></segment></net>
<net name="N32" class="0"><segment><pinref part="U1" gate="G$1" pin="RA4@12"/><wire x1="187.96" y1="-157.48" x2="182.88" y2="-157.48" width="0.1524" layer="91"/><label x="177.58" y="-157.08" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="11"/><wire x1="53.34" y1="-142.24" x2="58.42" y2="-142.24" width="0.1524" layer="91"/><label x="58.92" y="-141.84" size="1.27" layer="95"/></segment></net>
<net name="LED" class="0"><segment><pinref part="U1" gate="G$1" pin="RB5@14"/><wire x1="187.96" y1="-162.56" x2="182.88" y2="-162.56" width="0.1524" layer="91"/><label x="177.58" y="-162.16" size="1.27" layer="95"/></segment><segment><pinref part="R5" gate="G$1" pin="1"/><wire x1="-5.08" y1="-83.82" x2="-10.16" y2="-83.82" width="0.1524" layer="91"/><label x="-15.46" y="-83.42" size="1.27" layer="95"/></segment></net>
<net name="LED_A" class="0"><segment><pinref part="R5" gate="G$1" pin="2"/><wire x1="5.08" y1="-83.82" x2="10.16" y2="-83.82" width="0.1524" layer="91"/><label x="10.66" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="D1" gate="G$1" pin="A"/><wire x1="223.52" y1="-30.48" x2="218.44" y2="-30.48" width="0.1524" layer="91"/><label x="209.94" y="-30.08" size="1.27" layer="95"/></segment></net>
<net name="+5V_ISO" class="0"><segment><pinref part="U3" gate="G$1" pin="7"/><wire x1="7.62" y1="-177.8" x2="12.7" y2="-177.8" width="0.1524" layer="91"/><label x="13.2" y="-177.4" size="1.27" layer="95"/></segment><segment><pinref part="C11" gate="G$1" pin="1"/><wire x1="177.8" y1="-15.24" x2="172.72" y2="-15.24" width="0.1524" layer="91"/><label x="161.02" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="8"/><wire x1="58.42" y1="-177.8" x2="63.5" y2="-177.8" width="0.1524" layer="91"/><label x="64" y="-177.4" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="7"/><wire x1="58.42" y1="-180.34" x2="63.5" y2="-180.34" width="0.1524" layer="91"/><label x="64" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="C12" gate="G$1" pin="1"/><wire x1="223.52" y1="-15.24" x2="218.44" y2="-15.24" width="0.1524" layer="91"/><label x="206.74" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="R7" gate="G$1" pin="1"/><wire x1="86.36" y1="-83.82" x2="81.28" y2="-83.82" width="0.1524" layer="91"/><label x="69.58" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="R8" gate="G$1" pin="1"/><wire x1="132.08" y1="-83.82" x2="127" y2="-83.82" width="0.1524" layer="91"/><label x="115.3" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="R11" gate="G$1" pin="1"/><wire x1="-5.08" y1="-99.06" x2="-10.16" y2="-99.06" width="0.1524" layer="91"/><label x="-21.86" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="U7" gate="G$1" pin="8"/><wire x1="210.82" y1="-177.8" x2="215.9" y2="-177.8" width="0.1524" layer="91"/><label x="216.4" y="-177.4" size="1.27" layer="95"/></segment><segment><pinref part="C14" gate="G$1" pin="1"/><wire x1="40.64" y1="-30.48" x2="35.56" y2="-30.48" width="0.1524" layer="91"/><label x="23.86" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="R12" gate="G$1" pin="1"/><wire x1="40.64" y1="-99.06" x2="35.56" y2="-99.06" width="0.1524" layer="91"/><label x="23.86" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="U8" gate="G$1" pin="8"/><wire x1="261.62" y1="-177.8" x2="266.7" y2="-177.8" width="0.1524" layer="91"/><label x="267.2" y="-177.4" size="1.27" layer="95"/></segment><segment><pinref part="U8" gate="G$1" pin="7"/><wire x1="261.62" y1="-180.34" x2="266.7" y2="-180.34" width="0.1524" layer="91"/><label x="267.2" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="C15" gate="G$1" pin="1"/><wire x1="86.36" y1="-30.48" x2="81.28" y2="-30.48" width="0.1524" layer="91"/><label x="69.58" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="R16" gate="G$1" pin="1"/><wire x1="223.52" y1="-99.06" x2="218.44" y2="-99.06" width="0.1524" layer="91"/><label x="206.74" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="R17" gate="G$1" pin="1"/><wire x1="-5.08" y1="-114.3" x2="-10.16" y2="-114.3" width="0.1524" layer="91"/><label x="-21.86" y="-113.9" size="1.27" layer="95"/></segment><segment><pinref part="R20" gate="G$1" pin="1"/><wire x1="132.08" y1="-114.3" x2="127" y2="-114.3" width="0.1524" layer="91"/><label x="115.3" y="-113.9" size="1.27" layer="95"/></segment><segment><pinref part="U11" gate="G$1" pin="8"/><wire x1="109.22" y1="-200.66" x2="114.3" y2="-200.66" width="0.1524" layer="91"/><label x="114.8" y="-200.26" size="1.27" layer="95"/></segment><segment><pinref part="C17" gate="G$1" pin="1"/><wire x1="177.8" y1="-30.48" x2="172.72" y2="-30.48" width="0.1524" layer="91"/><label x="161.02" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="R21" gate="G$1" pin="1"/><wire x1="177.8" y1="-114.3" x2="172.72" y2="-114.3" width="0.1524" layer="91"/><label x="161.02" y="-113.9" size="1.27" layer="95"/></segment></net>
<net name="GND2" class="0"><segment><pinref part="U3" gate="G$1" pin="5"/><wire x1="7.62" y1="-180.34" x2="12.7" y2="-180.34" width="0.1524" layer="91"/><label x="13.2" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="C11" gate="G$1" pin="2"/><wire x1="187.96" y1="-15.24" x2="193.04" y2="-15.24" width="0.1524" layer="91"/><label x="193.54" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="5"/><wire x1="58.42" y1="-185.42" x2="63.5" y2="-185.42" width="0.1524" layer="91"/><label x="64" y="-185.02" size="1.27" layer="95"/></segment><segment><pinref part="C12" gate="G$1" pin="2"/><wire x1="233.68" y1="-15.24" x2="238.76" y2="-15.24" width="0.1524" layer="91"/><label x="239.26" y="-14.84" size="1.27" layer="95"/></segment><segment><pinref part="U6" gate="G$1" pin="3"/><wire x1="160.02" y1="-180.34" x2="165.1" y2="-180.34" width="0.1524" layer="91"/><label x="165.6" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="U7" gate="G$1" pin="5"/><wire x1="210.82" y1="-185.42" x2="215.9" y2="-185.42" width="0.1524" layer="91"/><label x="216.4" y="-185.02" size="1.27" layer="95"/></segment><segment><pinref part="C14" gate="G$1" pin="2"/><wire x1="50.8" y1="-30.48" x2="55.88" y2="-30.48" width="0.1524" layer="91"/><label x="56.38" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="R14" gate="G$1" pin="2"/><wire x1="142.24" y1="-99.06" x2="147.32" y2="-99.06" width="0.1524" layer="91"/><label x="147.82" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="D2" gate="G$1" pin="2"/><wire x1="5.08" y1="-45.72" x2="10.16" y2="-45.72" width="0.1524" layer="91"/><label x="10.66" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="D3" gate="G$1" pin="2"/><wire x1="50.8" y1="-45.72" x2="55.88" y2="-45.72" width="0.1524" layer="91"/><label x="56.38" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="J3" gate="G$1" pin="1"/><wire x1="-7.62" y1="-66.04" x2="-12.7" y2="-66.04" width="0.1524" layer="91"/><label x="-19.6" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="U8" gate="G$1" pin="5"/><wire x1="261.62" y1="-185.42" x2="266.7" y2="-185.42" width="0.1524" layer="91"/><label x="267.2" y="-185.02" size="1.27" layer="95"/></segment><segment><pinref part="C15" gate="G$1" pin="2"/><wire x1="96.52" y1="-30.48" x2="101.6" y2="-30.48" width="0.1524" layer="91"/><label x="102.1" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="U10" gate="G$1" pin="3"/><wire x1="58.42" y1="-203.2" x2="63.5" y2="-203.2" width="0.1524" layer="91"/><label x="64" y="-202.8" size="1.27" layer="95"/></segment><segment><pinref part="U11" gate="G$1" pin="5"/><wire x1="109.22" y1="-208.28" x2="114.3" y2="-208.28" width="0.1524" layer="91"/><label x="114.8" y="-207.88" size="1.27" layer="95"/></segment><segment><pinref part="C17" gate="G$1" pin="2"/><wire x1="187.96" y1="-30.48" x2="193.04" y2="-30.48" width="0.1524" layer="91"/><label x="193.54" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="R23" gate="G$1" pin="2"/><wire x1="5.08" y1="-129.54" x2="10.16" y2="-129.54" width="0.1524" layer="91"/><label x="10.66" y="-129.14" size="1.27" layer="95"/></segment><segment><pinref part="D4" gate="G$1" pin="2"/><wire x1="96.52" y1="-45.72" x2="101.6" y2="-45.72" width="0.1524" layer="91"/><label x="102.1" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="D5" gate="G$1" pin="2"/><wire x1="142.24" y1="-45.72" x2="147.32" y2="-45.72" width="0.1524" layer="91"/><label x="147.82" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="J4" gate="G$1" pin="1"/><wire x1="43.18" y1="-66.04" x2="38.1" y2="-66.04" width="0.1524" layer="91"/><label x="31.2" y="-65.64" size="1.27" layer="95"/></segment></net>
<net name="TX1" class="0"><segment><pinref part="U1" gate="G$1" pin="RB15/U1TX@26"/><wire x1="208.28" y1="-134.62" x2="213.36" y2="-134.62" width="0.1524" layer="91"/><label x="213.86" y="-134.22" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="3"/><wire x1="43.18" y1="-182.88" x2="38.1" y2="-182.88" width="0.1524" layer="91"/><label x="32.8" y="-182.48" size="1.27" layer="95"/></segment></net>
<net name="TX1_LED" class="0"><segment><pinref part="R6" gate="G$1" pin="2"/><wire x1="50.8" y1="-83.82" x2="55.88" y2="-83.82" width="0.1524" layer="91"/><label x="56.38" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="2"/><wire x1="43.18" y1="-180.34" x2="38.1" y2="-180.34" width="0.1524" layer="91"/><label x="26.4" y="-179.94" size="1.27" layer="95"/></segment></net>
<net name="RX1" class="0"><segment><pinref part="U1" gate="G$1" pin="RB13/U1RX@24"/><wire x1="208.28" y1="-139.7" x2="213.36" y2="-139.7" width="0.1524" layer="91"/><label x="213.86" y="-139.3" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="6"/><wire x1="109.22" y1="-182.88" x2="114.3" y2="-182.88" width="0.1524" layer="91"/><label x="114.8" y="-182.48" size="1.27" layer="95"/></segment><segment><pinref part="R9" gate="G$1" pin="2"/><wire x1="187.96" y1="-83.82" x2="193.04" y2="-83.82" width="0.1524" layer="91"/><label x="193.54" y="-83.42" size="1.27" layer="95"/></segment></net>
<net name="DIR1" class="0"><segment><pinref part="U1" gate="G$1" pin="RB14@25"/><wire x1="208.28" y1="-137.16" x2="213.36" y2="-137.16" width="0.1524" layer="91"/><label x="213.86" y="-136.76" size="1.27" layer="95"/></segment><segment><pinref part="R10" gate="G$1" pin="1"/><wire x1="223.52" y1="-83.82" x2="218.44" y2="-83.82" width="0.1524" layer="91"/><label x="211.54" y="-83.42" size="1.27" layer="95"/></segment></net>
<net name="DIR1_LED" class="0"><segment><pinref part="R10" gate="G$1" pin="2"/><wire x1="233.68" y1="-83.82" x2="238.76" y2="-83.82" width="0.1524" layer="91"/><label x="239.26" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="U6" gate="G$1" pin="1"/><wire x1="144.78" y1="-177.8" x2="139.7" y2="-177.8" width="0.1524" layer="91"/><label x="126.4" y="-177.4" size="1.27" layer="95"/></segment></net>
<net name="DI1" class="0"><segment><pinref part="U4" gate="G$1" pin="6"/><wire x1="58.42" y1="-182.88" x2="63.5" y2="-182.88" width="0.1524" layer="91"/><label x="64" y="-182.48" size="1.27" layer="95"/></segment><segment><pinref part="R7" gate="G$1" pin="2"/><wire x1="96.52" y1="-83.82" x2="101.6" y2="-83.82" width="0.1524" layer="91"/><label x="102.1" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="U7" gate="G$1" pin="4"/><wire x1="195.58" y1="-185.42" x2="190.5" y2="-185.42" width="0.1524" layer="91"/><label x="185.2" y="-185.02" size="1.27" layer="95"/></segment></net>
<net name="RO1" class="0"><segment><pinref part="U7" gate="G$1" pin="1"/><wire x1="195.58" y1="-177.8" x2="190.5" y2="-177.8" width="0.1524" layer="91"/><label x="185.2" y="-177.4" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="3"/><wire x1="93.98" y1="-182.88" x2="88.9" y2="-182.88" width="0.1524" layer="91"/><label x="83.6" y="-182.48" size="1.27" layer="95"/></segment></net>
<net name="RO1_LED" class="0"><segment><pinref part="R8" gate="G$1" pin="2"/><wire x1="142.24" y1="-83.82" x2="147.32" y2="-83.82" width="0.1524" layer="91"/><label x="147.82" y="-83.42" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="2"/><wire x1="93.98" y1="-180.34" x2="88.9" y2="-180.34" width="0.1524" layer="91"/><label x="77.2" y="-179.94" size="1.27" layer="95"/></segment></net>
<net name="DE1" class="0"><segment><pinref part="U6" gate="G$1" pin="4"/><wire x1="160.02" y1="-177.8" x2="165.1" y2="-177.8" width="0.1524" layer="91"/><label x="165.6" y="-177.4" size="1.27" layer="95"/></segment><segment><pinref part="R11" gate="G$1" pin="2"/><wire x1="5.08" y1="-99.06" x2="10.16" y2="-99.06" width="0.1524" layer="91"/><label x="10.66" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="U7" gate="G$1" pin="2"/><wire x1="195.58" y1="-180.34" x2="190.5" y2="-180.34" width="0.1524" layer="91"/><label x="185.2" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="U7" gate="G$1" pin="3"/><wire x1="195.58" y1="-182.88" x2="190.5" y2="-182.88" width="0.1524" layer="91"/><label x="185.2" y="-182.48" size="1.27" layer="95"/></segment></net>
<net name="A1" class="0"><segment><pinref part="U7" gate="G$1" pin="6"/><wire x1="210.82" y1="-182.88" x2="215.9" y2="-182.88" width="0.1524" layer="91"/><label x="216.4" y="-182.48" size="1.27" layer="95"/></segment><segment><pinref part="R12" gate="G$1" pin="2"/><wire x1="50.8" y1="-99.06" x2="55.88" y2="-99.06" width="0.1524" layer="91"/><label x="56.38" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="R13" gate="G$1" pin="1"/><wire x1="86.36" y1="-99.06" x2="81.28" y2="-99.06" width="0.1524" layer="91"/><label x="77.58" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="D2" gate="G$1" pin="1"/><wire x1="-5.08" y1="-45.72" x2="-10.16" y2="-45.72" width="0.1524" layer="91"/><label x="-13.86" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="J3" gate="G$1" pin="3"/><wire x1="7.62" y1="-66.04" x2="12.7" y2="-66.04" width="0.1524" layer="91"/><label x="13.2" y="-65.64" size="1.27" layer="95"/></segment></net>
<net name="B1" class="0"><segment><pinref part="U7" gate="G$1" pin="7"/><wire x1="210.82" y1="-180.34" x2="215.9" y2="-180.34" width="0.1524" layer="91"/><label x="216.4" y="-179.94" size="1.27" layer="95"/></segment><segment><pinref part="R13" gate="G$1" pin="2"/><wire x1="96.52" y1="-99.06" x2="101.6" y2="-99.06" width="0.1524" layer="91"/><label x="102.1" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="R14" gate="G$1" pin="1"/><wire x1="132.08" y1="-99.06" x2="127" y2="-99.06" width="0.1524" layer="91"/><label x="123.3" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="D3" gate="G$1" pin="1"/><wire x1="40.64" y1="-45.72" x2="35.56" y2="-45.72" width="0.1524" layer="91"/><label x="31.86" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="J3" gate="G$1" pin="2"/><wire x1="-7.62" y1="-68.58" x2="-12.7" y2="-68.58" width="0.1524" layer="91"/><label x="-16.4" y="-68.18" size="1.27" layer="95"/></segment></net>
<net name="TX2" class="0"><segment><pinref part="U1" gate="G$1" pin="RB9/U2TX@18"/><wire x1="208.28" y1="-154.94" x2="213.36" y2="-154.94" width="0.1524" layer="91"/><label x="213.86" y="-154.54" size="1.27" layer="95"/></segment><segment><pinref part="U8" gate="G$1" pin="3"/><wire x1="246.38" y1="-182.88" x2="241.3" y2="-182.88" width="0.1524" layer="91"/><label x="236" y="-182.48" size="1.27" layer="95"/></segment></net>
<net name="TX2_LED" class="0"><segment><pinref part="R15" gate="G$1" pin="2"/><wire x1="187.96" y1="-99.06" x2="193.04" y2="-99.06" width="0.1524" layer="91"/><label x="193.54" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="U8" gate="G$1" pin="2"/><wire x1="246.38" y1="-180.34" x2="241.3" y2="-180.34" width="0.1524" layer="91"/><label x="229.6" y="-179.94" size="1.27" layer="95"/></segment></net>
<net name="RX2" class="0"><segment><pinref part="U1" gate="G$1" pin="RB8/U2RX@17"/><wire x1="208.28" y1="-157.48" x2="213.36" y2="-157.48" width="0.1524" layer="91"/><label x="213.86" y="-157.08" size="1.27" layer="95"/></segment><segment><pinref part="U9" gate="G$1" pin="6"/><wire x1="7.62" y1="-205.74" x2="12.7" y2="-205.74" width="0.1524" layer="91"/><label x="13.2" y="-205.34" size="1.27" layer="95"/></segment><segment><pinref part="R18" gate="G$1" pin="2"/><wire x1="50.8" y1="-114.3" x2="55.88" y2="-114.3" width="0.1524" layer="91"/><label x="56.38" y="-113.9" size="1.27" layer="95"/></segment></net>
<net name="DIR2" class="0"><segment><pinref part="U1" gate="G$1" pin="RB7@16"/><wire x1="208.28" y1="-160.02" x2="213.36" y2="-160.02" width="0.1524" layer="91"/><label x="213.86" y="-159.62" size="1.27" layer="95"/></segment><segment><pinref part="R19" gate="G$1" pin="1"/><wire x1="86.36" y1="-114.3" x2="81.28" y2="-114.3" width="0.1524" layer="91"/><label x="74.38" y="-113.9" size="1.27" layer="95"/></segment></net>
<net name="DIR2_LED" class="0"><segment><pinref part="R19" gate="G$1" pin="2"/><wire x1="96.52" y1="-114.3" x2="101.6" y2="-114.3" width="0.1524" layer="91"/><label x="102.1" y="-113.9" size="1.27" layer="95"/></segment><segment><pinref part="U10" gate="G$1" pin="1"/><wire x1="43.18" y1="-200.66" x2="38.1" y2="-200.66" width="0.1524" layer="91"/><label x="24.8" y="-200.26" size="1.27" layer="95"/></segment></net>
<net name="DI2" class="0"><segment><pinref part="U8" gate="G$1" pin="6"/><wire x1="261.62" y1="-182.88" x2="266.7" y2="-182.88" width="0.1524" layer="91"/><label x="267.2" y="-182.48" size="1.27" layer="95"/></segment><segment><pinref part="R16" gate="G$1" pin="2"/><wire x1="233.68" y1="-99.06" x2="238.76" y2="-99.06" width="0.1524" layer="91"/><label x="239.26" y="-98.66" size="1.27" layer="95"/></segment><segment><pinref part="U11" gate="G$1" pin="4"/><wire x1="93.98" y1="-208.28" x2="88.9" y2="-208.28" width="0.1524" layer="91"/><label x="83.6" y="-207.88" size="1.27" layer="95"/></segment></net>
<net name="RO2" class="0"><segment><pinref part="U11" gate="G$1" pin="1"/><wire x1="93.98" y1="-200.66" x2="88.9" y2="-200.66" width="0.1524" layer="91"/><label x="83.6" y="-200.26" size="1.27" layer="95"/></segment><segment><pinref part="U9" gate="G$1" pin="3"/><wire x1="-7.62" y1="-205.74" x2="-12.7" y2="-205.74" width="0.1524" layer="91"/><label x="-18" y="-205.34" size="1.27" layer="95"/></segment></net>
<net name="RO2_LED" class="0"><segment><pinref part="R17" gate="G$1" pin="2"/><wire x1="5.08" y1="-114.3" x2="10.16" y2="-114.3" width="0.1524" layer="91"/><label x="10.66" y="-113.9" size="1.27" layer="95"/></segment><segment><pinref part="U9" gate="G$1" pin="2"/><wire x1="-7.62" y1="-203.2" x2="-12.7" y2="-203.2" width="0.1524" layer="91"/><label x="-24.4" y="-202.8" size="1.27" layer="95"/></segment></net>
<net name="DE2" class="0"><segment><pinref part="U10" gate="G$1" pin="4"/><wire x1="58.42" y1="-200.66" x2="63.5" y2="-200.66" width="0.1524" layer="91"/><label x="64" y="-200.26" size="1.27" layer="95"/></segment><segment><pinref part="R20" gate="G$1" pin="2"/><wire x1="142.24" y1="-114.3" x2="147.32" y2="-114.3" width="0.1524" layer="91"/><label x="147.82" y="-113.9" size="1.27" layer="95"/></segment><segment><pinref part="U11" gate="G$1" pin="2"/><wire x1="93.98" y1="-203.2" x2="88.9" y2="-203.2" width="0.1524" layer="91"/><label x="83.6" y="-202.8" size="1.27" layer="95"/></segment><segment><pinref part="U11" gate="G$1" pin="3"/><wire x1="93.98" y1="-205.74" x2="88.9" y2="-205.74" width="0.1524" layer="91"/><label x="83.6" y="-205.34" size="1.27" layer="95"/></segment></net>
<net name="A2" class="0"><segment><pinref part="U11" gate="G$1" pin="6"/><wire x1="109.22" y1="-205.74" x2="114.3" y2="-205.74" width="0.1524" layer="91"/><label x="114.8" y="-205.34" size="1.27" layer="95"/></segment><segment><pinref part="R21" gate="G$1" pin="2"/><wire x1="187.96" y1="-114.3" x2="193.04" y2="-114.3" width="0.1524" layer="91"/><label x="193.54" y="-113.9" size="1.27" layer="95"/></segment><segment><pinref part="R22" gate="G$1" pin="1"/><wire x1="223.52" y1="-114.3" x2="218.44" y2="-114.3" width="0.1524" layer="91"/><label x="214.74" y="-113.9" size="1.27" layer="95"/></segment><segment><pinref part="D4" gate="G$1" pin="1"/><wire x1="86.36" y1="-45.72" x2="81.28" y2="-45.72" width="0.1524" layer="91"/><label x="77.58" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="J4" gate="G$1" pin="3"/><wire x1="58.42" y1="-66.04" x2="63.5" y2="-66.04" width="0.1524" layer="91"/><label x="64" y="-65.64" size="1.27" layer="95"/></segment></net>
<net name="B2" class="0"><segment><pinref part="U11" gate="G$1" pin="7"/><wire x1="109.22" y1="-203.2" x2="114.3" y2="-203.2" width="0.1524" layer="91"/><label x="114.8" y="-202.8" size="1.27" layer="95"/></segment><segment><pinref part="R22" gate="G$1" pin="2"/><wire x1="233.68" y1="-114.3" x2="238.76" y2="-114.3" width="0.1524" layer="91"/><label x="239.26" y="-113.9" size="1.27" layer="95"/></segment><segment><pinref part="R23" gate="G$1" pin="1"/><wire x1="-5.08" y1="-129.54" x2="-10.16" y2="-129.54" width="0.1524" layer="91"/><label x="-13.86" y="-129.14" size="1.27" layer="95"/></segment><segment><pinref part="D5" gate="G$1" pin="1"/><wire x1="132.08" y1="-45.72" x2="127" y2="-45.72" width="0.1524" layer="91"/><label x="123.3" y="-45.32" size="1.27" layer="95"/></segment><segment><pinref part="J4" gate="G$1" pin="2"/><wire x1="43.18" y1="-68.58" x2="38.1" y2="-68.58" width="0.1524" layer="91"/><label x="34.4" y="-68.18" size="1.27" layer="95"/></segment></net>
</nets>
</sheet>
</sheets>
</schematic>
</drawing>
</eagle>
