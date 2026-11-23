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
<package name="PICO">
<pad name="1" x="0" y="0" drill="1" diameter="1.7" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="1" diameter="1.7" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="1" diameter="1.7" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="1" diameter="1.7" shape="round"/>
<pad name="5" x="0" y="-10.16" drill="1" diameter="1.7" shape="round"/>
<pad name="6" x="0" y="-12.7" drill="1" diameter="1.7" shape="round"/>
<pad name="7" x="0" y="-15.24" drill="1" diameter="1.7" shape="round"/>
<pad name="8" x="0" y="-17.78" drill="1" diameter="1.7" shape="round"/>
<pad name="9" x="0" y="-20.32" drill="1" diameter="1.7" shape="round"/>
<pad name="10" x="0" y="-22.86" drill="1" diameter="1.7" shape="round"/>
<pad name="11" x="0" y="-25.4" drill="1" diameter="1.7" shape="round"/>
<pad name="12" x="0" y="-27.94" drill="1" diameter="1.7" shape="round"/>
<pad name="13" x="0" y="-30.48" drill="1" diameter="1.7" shape="round"/>
<pad name="14" x="0" y="-33.02" drill="1" diameter="1.7" shape="round"/>
<pad name="15" x="0" y="-35.56" drill="1" diameter="1.7" shape="round"/>
<pad name="16" x="0" y="-38.1" drill="1" diameter="1.7" shape="round"/>
<pad name="17" x="0" y="-40.64" drill="1" diameter="1.7" shape="round"/>
<pad name="18" x="0" y="-43.18" drill="1" diameter="1.7" shape="round"/>
<pad name="19" x="0" y="-45.72" drill="1" diameter="1.7" shape="round"/>
<pad name="20" x="0" y="-48.26" drill="1" diameter="1.7" shape="round"/>
<pad name="21" x="17.78" y="-48.26" drill="1" diameter="1.7" shape="round"/>
<pad name="22" x="17.78" y="-45.72" drill="1" diameter="1.7" shape="round"/>
<pad name="23" x="17.78" y="-43.18" drill="1" diameter="1.7" shape="round"/>
<pad name="24" x="17.78" y="-40.64" drill="1" diameter="1.7" shape="round"/>
<pad name="25" x="17.78" y="-38.1" drill="1" diameter="1.7" shape="round"/>
<pad name="26" x="17.78" y="-35.56" drill="1" diameter="1.7" shape="round"/>
<pad name="27" x="17.78" y="-33.02" drill="1" diameter="1.7" shape="round"/>
<pad name="28" x="17.78" y="-30.48" drill="1" diameter="1.7" shape="round"/>
<pad name="29" x="17.78" y="-27.94" drill="1" diameter="1.7" shape="round"/>
<pad name="30" x="17.78" y="-25.4" drill="1" diameter="1.7" shape="round"/>
<pad name="31" x="17.78" y="-22.86" drill="1" diameter="1.7" shape="round"/>
<pad name="32" x="17.78" y="-20.32" drill="1" diameter="1.7" shape="round"/>
<pad name="33" x="17.78" y="-17.78" drill="1" diameter="1.7" shape="round"/>
<pad name="34" x="17.78" y="-15.24" drill="1" diameter="1.7" shape="round"/>
<pad name="35" x="17.78" y="-12.7" drill="1" diameter="1.7" shape="round"/>
<pad name="36" x="17.78" y="-10.16" drill="1" diameter="1.7" shape="round"/>
<pad name="37" x="17.78" y="-7.62" drill="1" diameter="1.7" shape="round"/>
<pad name="38" x="17.78" y="-5.08" drill="1" diameter="1.7" shape="round"/>
<pad name="39" x="17.78" y="-2.54" drill="1" diameter="1.7" shape="round"/>
<pad name="40" x="17.78" y="0" drill="1" diameter="1.7" shape="round"/>
<wire x1="-1.5" y1="-49.76" x2="19.28" y2="-49.76" width="0.2032" layer="21"/>
<wire x1="19.28" y1="-49.76" x2="19.28" y2="1.5" width="0.2032" layer="21"/>
<wire x1="19.28" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-49.76" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-51.66" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="BUTTON_12MM">
<pad name="G1" x="0" y="0" drill="1.1" diameter="2.4" shape="square"/>
<pad name="G2" x="0" y="-12.5" drill="1.1" diameter="2.4" shape="round"/>
<pad name="S1" x="-5" y="0" drill="1.1" diameter="2.4" shape="round"/>
<pad name="S2" x="-5" y="-12.5" drill="1.1" diameter="2.4" shape="round"/>
<wire x1="-6.5" y1="-14" x2="1.5" y2="-14" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-14" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-6.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-6.5" y1="1.5" x2="-6.5" y2="-14" width="0.2032" layer="21"/>
<text x="-6.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-6.5" y="-15.9" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="R_10.16">
<pad name="A" x="0" y="0" drill="0.9" diameter="2.3" shape="square"/>
<pad name="B" x="10.16" y="0" drill="0.9" diameter="2.3" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="11.66" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="11.66" y1="-1.5" x2="11.66" y2="1.5" width="0.2032" layer="21"/>
<wire x1="11.66" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="CP_2.00">
<pad name="P" x="0" y="0" drill="0.9" diameter="1.6" shape="square"/>
<pad name="N" x="2" y="0" drill="0.9" diameter="1.6" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="3.5" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="3.5" y1="-1.5" x2="3.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="3.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="6N137">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.7" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.7" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="0.9" diameter="1.7" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="0.9" diameter="1.7" shape="round"/>
<pad name="5" x="7.62" y="-7.62" drill="0.9" diameter="1.7" shape="round"/>
<pad name="6" x="7.62" y="-5.08" drill="0.9" diameter="1.7" shape="round"/>
<pad name="7" x="7.62" y="-2.54" drill="0.9" diameter="1.7" shape="round"/>
<pad name="8" x="7.62" y="0" drill="0.9" diameter="1.7" shape="round"/>
<wire x1="-1.5" y1="-9.12" x2="9.12" y2="-9.12" width="0.2032" layer="21"/>
<wire x1="9.12" y1="-9.12" x2="9.12" y2="1.5" width="0.2032" layer="21"/>
<wire x1="9.12" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-9.12" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-11.02" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="SN75176">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.7" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.7" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="0.9" diameter="1.7" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="0.9" diameter="1.7" shape="round"/>
<pad name="5" x="7.62" y="-7.62" drill="0.9" diameter="1.7" shape="round"/>
<pad name="6" x="7.62" y="-5.08" drill="0.9" diameter="1.7" shape="round"/>
<pad name="7" x="7.62" y="-2.54" drill="0.9" diameter="1.7" shape="round"/>
<pad name="8" x="7.62" y="0" drill="0.9" diameter="1.7" shape="round"/>
<wire x1="-1.5" y1="-9.12" x2="9.12" y2="-9.12" width="0.2032" layer="21"/>
<wire x1="9.12" y1="-9.12" x2="9.12" y2="1.5" width="0.2032" layer="21"/>
<wire x1="9.12" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-9.12" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-11.02" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="C_5.00">
<pad name="A" x="0" y="0" drill="0.9" diameter="2.3" shape="square"/>
<pad name="B" x="5" y="0" drill="0.9" diameter="2.3" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="6.5" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="6.5" y1="-1.5" x2="6.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="6.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="DO15_10.16">
<pad name="A" x="0" y="0" drill="1.1" diameter="2.4" shape="square"/>
<pad name="B" x="10.16" y="0" drill="1.1" diameter="2.4" shape="round"/>
<wire x1="-1.5" y1="-1.5" x2="11.66" y2="-1.5" width="0.2032" layer="21"/>
<wire x1="11.66" y1="-1.5" x2="11.66" y2="1.5" width="0.2032" layer="21"/>
<wire x1="11.66" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-1.5" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-3.4" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="TERMINAL_3">
<pad name="2" x="0" y="0" drill="1.3" diameter="2.6" shape="square"/>
<pad name="1" x="0" y="-5.08" drill="1.3" diameter="2.6" shape="round"/>
<pad name="3" x="0" y="-10.16" drill="1.3" diameter="2.6" shape="round"/>
<wire x1="-1.5" y1="-11.66" x2="1.5" y2="-11.66" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-11.66" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-11.66" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-13.56" size="1.27" layer="27">&gt;VALUE</text>
</package>
<package name="RKK_SIP7">
<pad name="1" x="0" y="0" drill="0.9" diameter="1.7" shape="square"/>
<pad name="2" x="0" y="-2.54" drill="0.9" diameter="1.7" shape="round"/>
<pad name="3" x="0" y="-5.08" drill="0.9" diameter="1.7" shape="round"/>
<pad name="4" x="0" y="-7.62" drill="0.9" diameter="1.7" shape="round"/>
<pad name="5" x="0" y="-10.16" drill="0.9" diameter="1.7" shape="round"/>
<pad name="6" x="0" y="-12.7" drill="0.9" diameter="1.7" shape="round"/>
<pad name="7" x="0" y="-15.24" drill="0.9" diameter="1.7" shape="round"/>
<wire x1="-1.5" y1="-16.74" x2="1.5" y2="-16.74" width="0.2032" layer="21"/>
<wire x1="1.5" y1="-16.74" x2="1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="1.5" y1="1.5" x2="-1.5" y2="1.5" width="0.2032" layer="21"/>
<wire x1="-1.5" y1="1.5" x2="-1.5" y2="-16.74" width="0.2032" layer="21"/>
<text x="-1.5" y="2.2" size="1.27" layer="25">&gt;NAME</text>
<text x="-1.5" y="-18.64" size="1.27" layer="27">&gt;VALUE</text>
</package>
</packages>
<symbols>
<symbol name="PICO">
<wire x1="-5.08" y1="-50.8" x2="5.08" y2="-50.8" width="0.254" layer="94"/>
<wire x1="5.08" y1="-50.8" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-50.8" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="-7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="4" x="-7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="5" x="-7.62" y="-10.16" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="6" x="-7.62" y="-12.7" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="7" x="-7.62" y="-15.24" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="8" x="-7.62" y="-17.78" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="9" x="-7.62" y="-20.32" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="10" x="-7.62" y="-22.86" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="11" x="-7.62" y="-25.4" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="12" x="-7.62" y="-27.94" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="13" x="-7.62" y="-30.48" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="14" x="-7.62" y="-33.02" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="15" x="-7.62" y="-35.56" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="16" x="-7.62" y="-38.1" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="17" x="-7.62" y="-40.64" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="18" x="-7.62" y="-43.18" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="19" x="-7.62" y="-45.72" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="20" x="-7.62" y="-48.26" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="21" x="7.62" y="-48.26" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="22" x="7.62" y="-45.72" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="23" x="7.62" y="-43.18" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="24" x="7.62" y="-40.64" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="25" x="7.62" y="-38.1" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="26" x="7.62" y="-35.56" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="27" x="7.62" y="-33.02" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="28" x="7.62" y="-30.48" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="29" x="7.62" y="-27.94" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="30" x="7.62" y="-25.4" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="31" x="7.62" y="-22.86" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="32" x="7.62" y="-20.32" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="33" x="7.62" y="-17.78" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="34" x="7.62" y="-15.24" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="35" x="7.62" y="-12.7" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="36" x="7.62" y="-10.16" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="37" x="7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="38" x="7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="39" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="40" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-53.6" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="BUTTON_12MM">
<wire x1="-5.08" y1="-5.08" x2="5.08" y2="-5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="-5.08" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-5.08" width="0.254" layer="94"/>
<pin name="G1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="G2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="S1" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="S2" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-7.88" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="R_10.16">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="A" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="B" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="CP_2.00">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="P" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="N" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="6N137">
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
<symbol name="SN75176">
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
<symbol name="C_5.00">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="A" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="B" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="DO15_10.16">
<wire x1="-2.54" y1="-1.27" x2="2.54" y2="-1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="-1.27" x2="2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="2.54" y1="1.27" x2="-2.54" y2="1.27" width="0.254" layer="94"/>
<wire x1="-2.54" y1="1.27" x2="-2.54" y2="-1.27" width="0.254" layer="94"/>
<pin name="A" x="-5.08" y="0" visible="pad" length="short" direction="pas" rot="R0"/>
<pin name="B" x="5.08" y="0" visible="pad" length="short" direction="pas" rot="R180"/>
<text x="-2.54" y="2.27" size="1.778" layer="95">&gt;NAME</text>
<text x="-2.54" y="-4.07" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="TERMINAL_3">
<wire x1="-5.08" y1="-5.08" x2="5.08" y2="-5.08" width="0.254" layer="94"/>
<wire x1="5.08" y1="-5.08" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-5.08" width="0.254" layer="94"/>
<pin name="2" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="1" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-7.88" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
<symbol name="RKK_SIP7">
<wire x1="-5.08" y1="-10.16" x2="5.08" y2="-10.16" width="0.254" layer="94"/>
<wire x1="5.08" y1="-10.16" x2="5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="5.08" y1="2.54" x2="-5.08" y2="2.54" width="0.254" layer="94"/>
<wire x1="-5.08" y1="2.54" x2="-5.08" y2="-10.16" width="0.254" layer="94"/>
<pin name="1" x="-7.62" y="0" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="2" x="-7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="3" x="-7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="4" x="-7.62" y="-7.62" visible="both" length="short" direction="pas" rot="R0"/>
<pin name="5" x="7.62" y="-5.08" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="6" x="7.62" y="-2.54" visible="both" length="short" direction="pas" rot="R180"/>
<pin name="7" x="7.62" y="0" visible="both" length="short" direction="pas" rot="R180"/>
<text x="-5.08" y="3.54" size="1.778" layer="95">&gt;NAME</text>
<text x="-5.08" y="-12.96" size="1.778" layer="96">&gt;VALUE</text>
</symbol>
</symbols>
<devicesets>
<deviceset name="PICO" prefix="U"><gates><gate name="G$1" symbol="PICO" x="0" y="0"/></gates><devices><device name="" package="PICO"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="6" pad="6"/><connect gate="G$1" pin="7" pad="7"/><connect gate="G$1" pin="8" pad="8"/><connect gate="G$1" pin="9" pad="9"/><connect gate="G$1" pin="10" pad="10"/><connect gate="G$1" pin="11" pad="11"/><connect gate="G$1" pin="12" pad="12"/><connect gate="G$1" pin="13" pad="13"/><connect gate="G$1" pin="14" pad="14"/><connect gate="G$1" pin="15" pad="15"/><connect gate="G$1" pin="16" pad="16"/><connect gate="G$1" pin="17" pad="17"/><connect gate="G$1" pin="18" pad="18"/><connect gate="G$1" pin="19" pad="19"/><connect gate="G$1" pin="20" pad="20"/><connect gate="G$1" pin="21" pad="21"/><connect gate="G$1" pin="22" pad="22"/><connect gate="G$1" pin="23" pad="23"/><connect gate="G$1" pin="24" pad="24"/><connect gate="G$1" pin="25" pad="25"/><connect gate="G$1" pin="26" pad="26"/><connect gate="G$1" pin="27" pad="27"/><connect gate="G$1" pin="28" pad="28"/><connect gate="G$1" pin="29" pad="29"/><connect gate="G$1" pin="30" pad="30"/><connect gate="G$1" pin="31" pad="31"/><connect gate="G$1" pin="32" pad="32"/><connect gate="G$1" pin="33" pad="33"/><connect gate="G$1" pin="34" pad="34"/><connect gate="G$1" pin="35" pad="35"/><connect gate="G$1" pin="36" pad="36"/><connect gate="G$1" pin="37" pad="37"/><connect gate="G$1" pin="38" pad="38"/><connect gate="G$1" pin="39" pad="39"/><connect gate="G$1" pin="40" pad="40"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="BUTTON_12MM" prefix="SW"><gates><gate name="G$1" symbol="BUTTON_12MM" x="0" y="0"/></gates><devices><device name="" package="BUTTON_12MM"><connects><connect gate="G$1" pin="G1" pad="G1"/><connect gate="G$1" pin="G2" pad="G2"/><connect gate="G$1" pin="S1" pad="S1"/><connect gate="G$1" pin="S2" pad="S2"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="R_10.16" prefix="R"><gates><gate name="G$1" symbol="R_10.16" x="0" y="0"/></gates><devices><device name="" package="R_10.16"><connects><connect gate="G$1" pin="A" pad="A"/><connect gate="G$1" pin="B" pad="B"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="CP_2.00" prefix="C"><gates><gate name="G$1" symbol="CP_2.00" x="0" y="0"/></gates><devices><device name="" package="CP_2.00"><connects><connect gate="G$1" pin="P" pad="P"/><connect gate="G$1" pin="N" pad="N"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="6N137" prefix="U"><gates><gate name="G$1" symbol="6N137" x="0" y="0"/></gates><devices><device name="" package="6N137"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="6" pad="6"/><connect gate="G$1" pin="7" pad="7"/><connect gate="G$1" pin="8" pad="8"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="SN75176" prefix="U"><gates><gate name="G$1" symbol="SN75176" x="0" y="0"/></gates><devices><device name="" package="SN75176"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="6" pad="6"/><connect gate="G$1" pin="7" pad="7"/><connect gate="G$1" pin="8" pad="8"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="C_5.00" prefix="C"><gates><gate name="G$1" symbol="C_5.00" x="0" y="0"/></gates><devices><device name="" package="C_5.00"><connects><connect gate="G$1" pin="A" pad="A"/><connect gate="G$1" pin="B" pad="B"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="DO15_10.16" prefix="D"><gates><gate name="G$1" symbol="DO15_10.16" x="0" y="0"/></gates><devices><device name="" package="DO15_10.16"><connects><connect gate="G$1" pin="A" pad="A"/><connect gate="G$1" pin="B" pad="B"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="TERMINAL_3" prefix="J"><gates><gate name="G$1" symbol="TERMINAL_3" x="0" y="0"/></gates><devices><device name="" package="TERMINAL_3"><connects><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="3" pad="3"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
<deviceset name="RKK_SIP7" prefix="U"><gates><gate name="G$1" symbol="RKK_SIP7" x="0" y="0"/></gates><devices><device name="" package="RKK_SIP7"><connects><connect gate="G$1" pin="1" pad="1"/><connect gate="G$1" pin="2" pad="2"/><connect gate="G$1" pin="3" pad="3"/><connect gate="G$1" pin="4" pad="4"/><connect gate="G$1" pin="5" pad="5"/><connect gate="G$1" pin="6" pad="6"/><connect gate="G$1" pin="7" pad="7"/></connects><technologies><technology name=""/></technologies></device></devices></deviceset>
</devicesets>
</library>
</libraries>
<attributes/>
<variantdefs/>
<classes><class number="0" name="default" width="0" drill="0"/></classes>
<parts>
<part name="U1" library="lumenet" deviceset="PICO" device="" value="Raspberry Pi Pico"/>
<part name="SW1" library="lumenet" deviceset="BUTTON_12MM" device="" value="tact switch 12 mm: blackout"/>
<part name="SW2" library="lumenet" deviceset="BUTTON_12MM" device="" value="tact switch 12 mm: preset 1"/>
<part name="R3" library="lumenet" deviceset="R_10.16" device="" value="180 Ω"/>
<part name="C4" library="lumenet" deviceset="CP_2.00" device="" value="4.7 µF"/>
<part name="U3" library="lumenet" deviceset="6N137" device="" value="6N137"/>
<part name="U5" library="lumenet" deviceset="SN75176" device="" value="SN75176BP"/>
<part name="C3" library="lumenet" deviceset="CP_2.00" device="" value="10 µF"/>
<part name="C1" library="lumenet" deviceset="C_5.00" device="" value="100 nF"/>
<part name="R4" library="lumenet" deviceset="R_10.16" device="" value="1 kΩ"/>
<part name="R2" library="lumenet" deviceset="R_10.16" device="" value="270 Ω"/>
<part name="C2" library="lumenet" deviceset="C_5.00" device="" value="100 nF"/>
<part name="D3" library="lumenet" deviceset="DO15_10.16" device="" value="P6KE6.8CA"/>
<part name="D1" library="lumenet" deviceset="DO15_10.16" device="" value="P6KE6.8CA"/>
<part name="J1" library="lumenet" deviceset="TERMINAL_3" device="" value="terminal 3 pole 5.08 mm"/>
<part name="U4" library="lumenet" deviceset="RKK_SIP7" device="" value="RKK-0505S/H"/>
</parts>
<sheets>
<sheet>
<plain><text x="0" y="25.4" size="2.54" layer="97">dmxbox: the box with the Pico (one DMX output)</text><text x="0" y="20.32" size="1.778" layer="97">Written by hardware/pico/schematic-1-universe/eagle_export.py from dmxbox_board.json.</text></plain>
<instances>
<instance part="U1" gate="G$1" x="0" y="0"/>
<instance part="SW1" gate="G$1" x="50.8" y="0"/>
<instance part="SW2" gate="G$1" x="101.6" y="0"/>
<instance part="R3" gate="G$1" x="152.4" y="0"/>
<instance part="C4" gate="G$1" x="198.12" y="0"/>
<instance part="U3" gate="G$1" x="243.84" y="0"/>
<instance part="U5" gate="G$1" x="0" y="-63.5"/>
<instance part="C3" gate="G$1" x="50.8" y="-63.5"/>
<instance part="C1" gate="G$1" x="96.52" y="-63.5"/>
<instance part="R4" gate="G$1" x="142.24" y="-63.5"/>
<instance part="R2" gate="G$1" x="187.96" y="-63.5"/>
<instance part="C2" gate="G$1" x="233.68" y="-63.5"/>
<instance part="D3" gate="G$1" x="0" y="-86.36"/>
<instance part="D1" gate="G$1" x="45.72" y="-86.36"/>
<instance part="J1" gate="G$1" x="91.44" y="-86.36"/>
<instance part="U4" gate="G$1" x="142.24" y="-86.36"/>
</instances>
<busses/>
<nets>
<net name="DMX_TX" class="0"><segment><pinref part="U1" gate="G$1" pin="1"/><wire x1="-7.62" y1="0" x2="-12.7" y2="0" width="0.1524" layer="91"/><label x="-22.8" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U3" gate="G$1" pin="3"/><wire x1="236.22" y1="-5.08" x2="231.14" y2="-5.08" width="0.1524" layer="91"/><label x="221.04" y="-4.68" size="1.27" layer="95"/></segment></net>
<net name="GND" class="0"><segment><pinref part="U1" gate="G$1" pin="23"/><wire x1="7.62" y1="-43.18" x2="12.7" y2="-43.18" width="0.1524" layer="91"/><label x="13.2" y="-42.78" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="28"/><wire x1="7.62" y1="-30.48" x2="12.7" y2="-30.48" width="0.1524" layer="91"/><label x="13.2" y="-30.08" size="1.27" layer="95"/></segment><segment><pinref part="U1" gate="G$1" pin="33"/><wire x1="7.62" y1="-17.78" x2="12.7" y2="-17.78" width="0.1524" layer="91"/><label x="13.2" y="-17.38" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="G1"/><wire x1="43.18" y1="0" x2="38.1" y2="0" width="0.1524" layer="91"/><label x="32.8" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="G2"/><wire x1="43.18" y1="-2.54" x2="38.1" y2="-2.54" width="0.1524" layer="91"/><label x="32.8" y="-2.14" size="1.27" layer="95"/></segment><segment><pinref part="SW2" gate="G$1" pin="G1"/><wire x1="93.98" y1="0" x2="88.9" y2="0" width="0.1524" layer="91"/><label x="83.6" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="SW2" gate="G$1" pin="G2"/><wire x1="93.98" y1="-2.54" x2="88.9" y2="-2.54" width="0.1524" layer="91"/><label x="83.6" y="-2.14" size="1.27" layer="95"/></segment><segment><pinref part="C4" gate="G$1" pin="N"/><wire x1="203.2" y1="0" x2="208.28" y2="0" width="0.1524" layer="91"/><label x="208.78" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="2"/><wire x1="134.62" y1="-88.9" x2="129.54" y2="-88.9" width="0.1524" layer="91"/><label x="124.24" y="-88.5" size="1.27" layer="95"/></segment></net>
<net name="BTN1" class="0"><segment><pinref part="U1" gate="G$1" pin="24"/><wire x1="7.62" y1="-40.64" x2="12.7" y2="-40.64" width="0.1524" layer="91"/><label x="13.2" y="-40.24" size="1.27" layer="95"/></segment><segment><pinref part="SW1" gate="G$1" pin="S1"/><wire x1="58.42" y1="-2.54" x2="63.5" y2="-2.54" width="0.1524" layer="91"/><label x="64" y="-2.14" size="1.27" layer="95"/></segment></net>
<net name="BTN2" class="0"><segment><pinref part="U1" gate="G$1" pin="25"/><wire x1="7.62" y1="-38.1" x2="12.7" y2="-38.1" width="0.1524" layer="91"/><label x="13.2" y="-37.7" size="1.27" layer="95"/></segment><segment><pinref part="SW2" gate="G$1" pin="S1"/><wire x1="109.22" y1="-2.54" x2="114.3" y2="-2.54" width="0.1524" layer="91"/><label x="114.8" y="-2.14" size="1.27" layer="95"/></segment></net>
<net name="3V3" class="0"><segment><pinref part="U1" gate="G$1" pin="36"/><wire x1="7.62" y1="-10.16" x2="12.7" y2="-10.16" width="0.1524" layer="91"/><label x="13.2" y="-9.76" size="1.27" layer="95"/></segment><segment><pinref part="R3" gate="G$1" pin="B"/><wire x1="157.48" y1="0" x2="162.56" y2="0" width="0.1524" layer="91"/><label x="163.06" y="0.4" size="1.27" layer="95"/></segment></net>
<net name="VSYS" class="0"><segment><pinref part="U1" gate="G$1" pin="39"/><wire x1="7.62" y1="-2.54" x2="12.7" y2="-2.54" width="0.1524" layer="91"/><label x="13.2" y="-2.14" size="1.27" layer="95"/></segment><segment><pinref part="C4" gate="G$1" pin="P"/><wire x1="193.04" y1="0" x2="187.96" y2="0" width="0.1524" layer="91"/><label x="181.06" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="1"/><wire x1="134.62" y1="-86.36" x2="129.54" y2="-86.36" width="0.1524" layer="91"/><label x="122.64" y="-85.96" size="1.27" layer="95"/></segment></net>
<net name="OPTO_A" class="0"><segment><pinref part="R3" gate="G$1" pin="A"/><wire x1="147.32" y1="0" x2="142.24" y2="0" width="0.1524" layer="91"/><label x="132.14" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U3" gate="G$1" pin="2"/><wire x1="236.22" y1="-2.54" x2="231.14" y2="-2.54" width="0.1524" layer="91"/><label x="221.04" y="-2.14" size="1.27" layer="95"/></segment></net>
<net name="GND2" class="0"><segment><pinref part="U3" gate="G$1" pin="5"/><wire x1="251.46" y1="-7.62" x2="256.54" y2="-7.62" width="0.1524" layer="91"/><label x="257.04" y="-7.22" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="5"/><wire x1="7.62" y1="-71.12" x2="12.7" y2="-71.12" width="0.1524" layer="91"/><label x="13.2" y="-70.72" size="1.27" layer="95"/></segment><segment><pinref part="C3" gate="G$1" pin="N"/><wire x1="55.88" y1="-63.5" x2="60.96" y2="-63.5" width="0.1524" layer="91"/><label x="61.46" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="C1" gate="G$1" pin="B"/><wire x1="101.6" y1="-63.5" x2="106.68" y2="-63.5" width="0.1524" layer="91"/><label x="107.18" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="R2" gate="G$1" pin="B"/><wire x1="193.04" y1="-63.5" x2="198.12" y2="-63.5" width="0.1524" layer="91"/><label x="198.62" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="C2" gate="G$1" pin="B"/><wire x1="238.76" y1="-63.5" x2="243.84" y2="-63.5" width="0.1524" layer="91"/><label x="244.34" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="D3" gate="G$1" pin="B"/><wire x1="5.08" y1="-86.36" x2="10.16" y2="-86.36" width="0.1524" layer="91"/><label x="10.66" y="-85.96" size="1.27" layer="95"/></segment><segment><pinref part="D1" gate="G$1" pin="B"/><wire x1="50.8" y1="-86.36" x2="55.88" y2="-86.36" width="0.1524" layer="91"/><label x="56.38" y="-85.96" size="1.27" layer="95"/></segment><segment><pinref part="J1" gate="G$1" pin="1"/><wire x1="83.82" y1="-88.9" x2="78.74" y2="-88.9" width="0.1524" layer="91"/><label x="71.84" y="-88.5" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="5"/><wire x1="149.86" y1="-91.44" x2="154.94" y2="-91.44" width="0.1524" layer="91"/><label x="155.44" y="-91.04" size="1.27" layer="95"/></segment></net>
<net name="OPTO_OUT" class="0"><segment><pinref part="U3" gate="G$1" pin="6"/><wire x1="251.46" y1="-5.08" x2="256.54" y2="-5.08" width="0.1524" layer="91"/><label x="257.04" y="-4.68" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="4"/><wire x1="-7.62" y1="-71.12" x2="-12.7" y2="-71.12" width="0.1524" layer="91"/><label x="-26" y="-70.72" size="1.27" layer="95"/></segment><segment><pinref part="R4" gate="G$1" pin="A"/><wire x1="137.16" y1="-63.5" x2="132.08" y2="-63.5" width="0.1524" layer="91"/><label x="118.78" y="-63.1" size="1.27" layer="95"/></segment></net>
<net name="5V_ISO" class="0"><segment><pinref part="U3" gate="G$1" pin="7"/><wire x1="251.46" y1="-2.54" x2="256.54" y2="-2.54" width="0.1524" layer="91"/><label x="257.04" y="-2.14" size="1.27" layer="95"/></segment><segment><pinref part="U3" gate="G$1" pin="8"/><wire x1="251.46" y1="0" x2="256.54" y2="0" width="0.1524" layer="91"/><label x="257.04" y="0.4" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="2"/><wire x1="-7.62" y1="-66.04" x2="-12.7" y2="-66.04" width="0.1524" layer="91"/><label x="-22.8" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="3"/><wire x1="-7.62" y1="-68.58" x2="-12.7" y2="-68.58" width="0.1524" layer="91"/><label x="-22.8" y="-68.18" size="1.27" layer="95"/></segment><segment><pinref part="U5" gate="G$1" pin="8"/><wire x1="7.62" y1="-63.5" x2="12.7" y2="-63.5" width="0.1524" layer="91"/><label x="13.2" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="C3" gate="G$1" pin="P"/><wire x1="45.72" y1="-63.5" x2="40.64" y2="-63.5" width="0.1524" layer="91"/><label x="30.54" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="C1" gate="G$1" pin="A"/><wire x1="91.44" y1="-63.5" x2="86.36" y2="-63.5" width="0.1524" layer="91"/><label x="76.26" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="R4" gate="G$1" pin="B"/><wire x1="147.32" y1="-63.5" x2="152.4" y2="-63.5" width="0.1524" layer="91"/><label x="152.9" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="R2" gate="G$1" pin="A"/><wire x1="182.88" y1="-63.5" x2="177.8" y2="-63.5" width="0.1524" layer="91"/><label x="167.7" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="C2" gate="G$1" pin="A"/><wire x1="228.6" y1="-63.5" x2="223.52" y2="-63.5" width="0.1524" layer="91"/><label x="213.42" y="-63.1" size="1.27" layer="95"/></segment><segment><pinref part="U4" gate="G$1" pin="7"/><wire x1="149.86" y1="-86.36" x2="154.94" y2="-86.36" width="0.1524" layer="91"/><label x="155.44" y="-85.96" size="1.27" layer="95"/></segment></net>
<net name="DMX_A" class="0"><segment><pinref part="U5" gate="G$1" pin="6"/><wire x1="7.62" y1="-68.58" x2="12.7" y2="-68.58" width="0.1524" layer="91"/><label x="13.2" y="-68.18" size="1.27" layer="95"/></segment><segment><pinref part="D1" gate="G$1" pin="A"/><wire x1="40.64" y1="-86.36" x2="35.56" y2="-86.36" width="0.1524" layer="91"/><label x="27.06" y="-85.96" size="1.27" layer="95"/></segment><segment><pinref part="J1" gate="G$1" pin="3"/><wire x1="99.06" y1="-86.36" x2="104.14" y2="-86.36" width="0.1524" layer="91"/><label x="104.64" y="-85.96" size="1.27" layer="95"/></segment></net>
<net name="DMX_B" class="0"><segment><pinref part="U5" gate="G$1" pin="7"/><wire x1="7.62" y1="-66.04" x2="12.7" y2="-66.04" width="0.1524" layer="91"/><label x="13.2" y="-65.64" size="1.27" layer="95"/></segment><segment><pinref part="D3" gate="G$1" pin="A"/><wire x1="-5.08" y1="-86.36" x2="-10.16" y2="-86.36" width="0.1524" layer="91"/><label x="-18.66" y="-85.96" size="1.27" layer="95"/></segment><segment><pinref part="J1" gate="G$1" pin="2"/><wire x1="83.82" y1="-86.36" x2="78.74" y2="-86.36" width="0.1524" layer="91"/><label x="70.24" y="-85.96" size="1.27" layer="95"/></segment></net>
</nets>
</sheet>
</sheets>
</schematic>
</drawing>
</eagle>
