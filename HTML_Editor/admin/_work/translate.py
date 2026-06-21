# -*- coding: utf-8 -*-
import json, re, os
os.chdir(os.path.dirname(os.path.abspath(__file__)))
src = json.load(open('translate_src.json', encoding='latin-1'))

RC = "�"  # matches the literal R="?" placeholder char used in PH keys below
def deno(s):
    if s is None: return None
    # map all Danish garble bytes to the single placeholder used in PH keys,
    # and en/em dash to '-'
    for ch in ('\xe6','\xc6','\xf8','\xd8','\xe5','\xc5','\xe9','\xc9'):
        s = s.replace(ch, RC)
    s = s.replace('\x96','-').replace('\x97','-').replace('\x92',"'")
    return s
for it in src:
    it['subtitle']=deno(it.get('subtitle'))
    it['headline']=deno(it.get('headline'))
    it['description']=deno(it.get('description'))
    it['features']=[deno(f) for f in (it.get('features') or [])]

SUBTITLE = {
 "SmartLine induktionsplade med PowerFlex-kogezone": "SmartLine induction element with PowerFlex cooking zone",
 "Induktionskogeplade GOLD": "Induction cooktop GOLD",
 "Induktionskogeplade SILVER": "Induction cooktop SILVER",
 "Glaskeramisk kogeplade SILVER": "Ceramic cooktop SILVER",
 "SmartLine gasblus med dual-wok-braender": "SmartLine gas burner with dual-wok burner",
 "SmartLine integreret bordemfang PLATINUM": "SmartLine integrated downdraft extractor PLATINUM",
 "Fritst�ende mikrob�lgeovn SILVER": "Freestanding microwave oven SILVER",
}
HOOD_SUB = {
 "Underbygningsemh�tte": "Built-under cooker hood",
 "Loftintegrerede emh�tter": "Ceiling-integrated cooker hood",
 "Bordemfang": "Downdraft extractor",
 "Indsatsemh�tte": "Canopy cooker hood",
 "V�gemh�tte": "Wall-mounted cooker hood",
 "Emh�tte til udtr�k": "Telescopic cooker hood",
 "Frith�ngende emh�tte": "Island cooker hood",
}

def tr_subtitle(s, cat):
    if s is None: return None
    if s in SUBTITLE: return SUBTITLE[s]
    if cat == 'Microwaves': return s
    for dk,en in HOOD_SUB.items():
        if s.startswith(dk):
            rest = s[len(dk):].strip()
            return (en + (" " + rest if rest else "")).strip()
    return s

R = "�"  # replacement char
PH = {
 "med induktionsvarmet PowerFlex-kogezone": "with induction-heated PowerFlex cooking zone",
 "med PowerFlex-kogeomraade og knapbetjening": "with PowerFlex cooking area and push-button control",
 "med 2 PowerFlex-kogeomraader til maksimal effekt": "with 2 PowerFlex cooking areas for maximum power",
 "med PowerFlex-kogeomraade til maksimal effekt": "with PowerFlex cooking area for maximum power",
 "med flex-kogeomraade til stort kogegrej": "with flex cooking area for large cookware",
 "med 4 kogezoner til stoerst mulig komfort": "with 4 cooking zones for the greatest possible comfort",
 "med 4 individuelle kogezoner til en attraktiv pris": "with 4 individual cooking zones at an attractive price",
 "Individuelle kogezoner og PowerFlex XL-kogeomraade": "Individual cooking zones and PowerFlex XL cooking area",
 "Individuelle kogezoner og PowerFlex-kogeomraade": "Individual cooking zones and PowerFlex cooking area",
 "PowerFlex-kogeomraader | M Sense ready": "PowerFlex cooking areas | M Sense ready",
 "med dual-wok-braender": "with dual-wok burner",
 "integreret bordemfang til udluftning til det fri eller recirkulation": "integrated downdraft extractor for exhaust or recirculation operation",
 "med betjeningsknapper i siden": "with control buttons on the side",
 "med energibesparende LED-belysning og skydeafbryder til nem betjening": "with energy-saving LED lighting and a slide switch for easy operation",
 "med energibesparende LED-belysning og elegant touch-betjening": "with energy-saving LED lighting and elegant touch control",
 "med energibesparende LED-belysning og taster til komfortabel betjening": "with energy-saving LED lighting and buttons for comfortable operation",
 "med energibesparende LED-belysning og touchbetjening til nem betjening": "with energy-saving LED lighting and touch control for easy operation",
 "med EasySwitch-taster til bekvem betjening og LED-belysning": "with EasySwitch buttons for convenient operation and LED lighting",
 "med EasySwitch-knapper til bekvem betjening": "with EasySwitch buttons for convenient operation",
 "med EasySwitch-styring til bekvem betjening": "with EasySwitch control for convenient operation",
 "med Con@ctivity og smart touch-betjening for komfortabel styring": "with Con@ctivity and smart touch control for comfortable operation",
 "med intuitiv SmartControl White-betjening til montering i smalle skabe": "with intuitive SmartControl White operation for installation in narrow cabinets",
 "med eksklusiv Blackboard-glasfront og intuitiv SmartControl-betjening": "with an exclusive Blackboard glass front and intuitive SmartControl operation",
 "med intuitiv SmartControl-betjening": "with intuitive SmartControl operation",
 "med lodret glasfront og s"+R+"rlig fladt design til frit udsyn": "with a vertical glass front and a particularly flat design for an unobstructed view",
 "til kombination med en ekstern bl"+R+"ser til reduceret lydniveau k"+R+"kkenet": "for combination with an external blower for a reduced noise level in the kitchen",
 "til kombination med en ekstern bl"+R+"ser": "for combination with an external blower",
 "i kompakt design i 880 mm bredde": "in a compact design, 880 mm wide",
 "med ambient-belysning, kompakt og kraftig i 1165 mm bredde": "with ambient lighting, compact and powerful, 1165 mm wide",
 "Motoriseret udtr"+R+"ksemh"+R+"tte - Hood in Motion": "Motorised pop-up extractor - Hood in Motion",
 "Elegant og effektiv "+R+" kantudsugningspaneler": "Elegant and effective - edge-extraction panels",
 "Intuitivt, hurtigt valg via talraekker - SmartSelect": "Intuitive, fast selection via number rows - SmartSelect",
 "Intuitivt og hurtigt valg med talraekke - ComfortSelect": "Intuitive and fast selection with a number row - ComfortSelect",
 "Elegant design - indbygget forsaenket i eller oven paa bordplade": "Elegant design - installed flush in or on top of the worktop",
 "Kan kombineres perfekt med alle SmartLine-elementer": "Can be perfectly combined with all SmartLine elements",
 "Fleksibel og hurtig - 2 kogezoner inkl. 1 PowerFlex-omraade": "Flexible and fast - 2 cooking zones incl. 1 PowerFlex area",
 "Meget korte opkogstider - TwinBooster": "Very short heat-up times - TwinBooster",
 "Direkte betjening - knap paa kogepladen": "Direct operation - button on the cooktop",
 "4 kogezoner inkl. 2 effektive PowerFlex-kogeomraader": "4 cooking zones incl. 2 efficient PowerFlex cooking areas",
 "4 kogezoner inkl. 1 effektivt PowerFlex XL-kogeomraade": "4 cooking zones incl. 1 efficient PowerFlex XL cooking area",
 "5 kogezoner inkl. 1 effektivt PowerFlex XL-kogeomraade": "5 cooking zones incl. 1 efficient PowerFlex XL cooking area",
 "4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade": "4 cooking zones incl. 1 efficient PowerFlex cooking area",
 "4 kogezoner, inkl. 1 flex-kogeomraade": "4 cooking zones, incl. 1 flex cooking area",
 "4 individuelle kogezoner i forskellige stoerrelser": "4 individual cooking zones in different sizes",
 "626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen": "626 mm wide with a stainless steel frame for installation on top of the worktop",
 "806 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen": "806 mm wide with a stainless steel frame for installation on top of the worktop",
 "620 mm bred til indbygning oven paa eller i plan med bordpladen": "620 mm wide for installation on top of or flush with the worktop",
 "800 mm bred til indbygning oven paa eller i plan med bordpladen": "800 mm wide for installation on top of or flush with the worktop",
 "626 mm bred til indbygning oven paa eller i plan med bordpladen": "626 mm wide for installation on top of or flush with the worktop",
 "752 mm bred til indbygning i plan med bordpladen": "752 mm wide for flush installation with the worktop",
 "592 mm bred til indbygning i plan med bordpladen": "592 mm wide for flush installation with the worktop",
 "Kommunikation med emhaetten - automatikfunktion Con@ctivity": "Communication with the cooker hood - automatic Con@ctivity function",
 "Netvaerkstilslutning med Con@ctivity og Miele@home": "Network connection with Con@ctivity and Miele@home",
 "Praktisk brug af stort kogegrej takket vaere kombinerbare Flex-kogezoner": "Practical use of large cookware thanks to combinable Flex cooking zones",
 "Praecise indstillinger via SingleSlide": "Precise settings via SingleSlide",
 "Praecis indstilling via 4 MultiSlide slidere og intelligent tilslutning": "Precise setting via 4 MultiSlide sliders and intelligent connection",
 "Korteste opvarmningstid takket vaere boostertrinnet": "Shortest heat-up time thanks to the booster level",
 "Til tilberedning med M Sense-kogegrej - M Sense ready": "For cooking with M Sense cookware - M Sense ready",
 "Enkel betjening - EasySelect": "Simple operation - EasySelect",
 "Indbydende design - 574 mm bred med ramme hele vejen rundt": "Inviting design - 574 mm wide with a frame all the way around",
 "Saerdeles fleksibel - inkl. 4 kogezoner 1 vario-zone": "Highly flexible - incl. 4 cooking zones and 1 vario zone",
 "Komfortabel - opkogsautomatik for hver enkelt kogezone": "Convenient - automatic boil-up for each individual cooking zone",
 "Sikker - indikator for restvarme med tre trin for hver kogezone": "Safe - residual heat indicator with three levels for each cooking zone",
 "Nem betjening med drejeknapper - kan betjenes med en haand": "Easy operation with rotary knobs - can be operated with one hand",
 "Nem rengoering - ComfortClean-rist til rengoering i opvaskemaskine": "Easy cleaning - ComfortClean grate, dishwasher-safe",
 "Effektiv filtrering - 10-lags metalfedtfilter i rustfrit staal": "Efficient filtration - 10-layer stainless steel metal grease filter",
 "Energibesparende og lydsvag - effektiv ECO-motor": "Energy-saving and quiet - efficient ECO motor",
 "Puristisk design - pladsbesparende med en bredde p"+R+" 598 mm": "Puristic design - space-saving with a width of 598 mm",
 "Effektiv filtrering "+R+" 10-lags metalfedtfilter i rustfrit st"+R+"l": "Efficient filtration - 10-layer stainless steel metal grease filter",
 "Effektiv filtrering - 10-lags metalfedtfilter": "Efficient filtration - 10-layer metal grease filter",
 "Sikker og nem at reng"+R+"re "+R+" Miele CleanCover": "Safe and easy to clean - Miele CleanCover",
 "Egnet til udluftning til det fri og recirkulation": "Suitable for exhaust and recirculation operation",
 "Nem installation ved recirkulation med plug&play": "Easy installation in recirculation mode with plug&play",
 "Nem installation ved recirkulation med Plug&Play-tilbeh"+R+"rss"+R+"t": "Easy installation in recirculation mode with a Plug&Play accessory kit",
 "Praktisk betjening - inkl. Con@ctivity og fjernbetjening": "Convenient operation - incl. Con@ctivity and remote control",
 "Energibesparende og lydsvag "+R+" effektiv ECO-motor": "Energy-saving and quiet - efficient ECO motor",
 "Individuelt just"+R+"rbart betjeningspanel": "Individually adjustable control panel",
 "ECO-motor med laveste lydniveau ved h"+R+"j luftydelse": "ECO motor with the lowest noise level at high air performance",
 "DynamicWhite - tilpasser farvetemperaturen, s"+R+" det harmonerer skiftende lyskilder": "DynamicWhite - adjusts the colour temperature to harmonise with changing light sources",
 "individuel, farvet belysning til en indbydende stemning": "individual coloured lighting for an inviting atmosphere",
 "916 mm bred - perfekt kombination med panorama-kogeplader": "916 mm wide - a perfect combination with panorama cooktops",
 "936 mm bred - perfekt i kombination med induktionskogeplader": "936 mm wide - a perfect combination with induction cooktops",
 "Enest"+R+"ende betjeningskomfort": "Outstanding operating comfort",
 "Fuldintegrerbar med en bredde p"+R+" 532 mm": "Fully integratable with a width of 532 mm",
 "Fuldintegrerbar med en bredde p"+R+" 584 mm": "Fully integratable with a width of 584 mm",
 "Fuldintegrerbar med en bredde p"+R+" 884 mm": "Fully integratable with a width of 884 mm",
 "Fuldintegrerbar med en bredde p"+R+" 702 mm": "Fully integratable with a width of 702 mm",
 "Fuldintegrerbar med en bredde p"+R+" 580 mm": "Fully integratable with a width of 580 mm",
 "Fuldintegrerbar med en bredde p"+R+" 880 mm": "Fully integratable with a width of 880 mm",
 "St"+R+"rk - p"+R+" h"+R+"jeste trin": "Powerful - at the highest level",
 "Ensartet belysning takket v"+R+"re LED-lysliste": "Uniform lighting thanks to an LED light strip",
 "Frit udsyn - med 598 mm bred glassk"+R+"rm": "Unobstructed view - with a 598 mm wide glass screen",
 "Frit udsyn - med 798 mm bred glassk"+R+"rm": "Unobstructed view - with a 798 mm wide glass screen",
 "Frit udsyn - med 898 mm bred glassk"+R+"rm": "Unobstructed view - with an 898 mm wide glass screen",
 "Frit udsyn - med frontpanel i 898 mm bredde": "Unobstructed view - with a front panel 898 mm wide",
 "Perfekt match - glaspanel og emfang i samme farve": "Perfect match - glass panel and hood in the same colour",
 "Effektiv - 545 m3/h i boostertrinnet": "Efficient - 545 m3/h at the booster level",
 "Effektiv - 585 m3/h i boostertrinnet": "Efficient - 585 m3/h at the booster level",
 "Effektiv - 600 m3/h i boostertrinnet": "Efficient - 600 m3/h at the booster level",
 "Effektiv - 630 m3/h i boostertrinnet": "Efficient - 630 m3/h at the booster level",
 "Effektiv - 635 m3/h i boostertrinnet": "Efficient - 635 m3/h at the booster level",
 "Effektiv - 640 m3/h i boostertrinnet": "Efficient - 640 m3/h at the booster level",
 "Effektiv - 645 m3/h i boostertrinnet": "Efficient - 645 m3/h at the booster level",
 "Effektiv - 650 m3/h i boostertrinnet": "Efficient - 650 m3/h at the booster level",
 "Effektiv - 720 m3/h i boostertrinnet": "Efficient - 720 m3/h at the booster level",
 "Effektiv - 730 m3/h i boostertrinnet": "Efficient - 730 m3/h at the booster level",
 "ECO-motor - den mest lydsvage Miele-emh"+R+"tte til frit udsyn med kun 50 dB": "ECO motor - the quietest Miele hood for an unobstructed view at just 50 dB",
 "Unik brugervenlighed - SmartControl og Con@ctivity": "Unique ease of use - SmartControl and Con@ctivity",
 "Netv"+R+"rkstilslutning med Con@ctivity og Miele@home": "Network connection with Con@ctivity and Miele@home",
 "Klassisk design - 60 cm bred emsk"+R+"rm af rustfrit st"+R+"l": "Classic design - 60 cm wide stainless steel canopy",
 "Klassisk design - 90 cm bred emsk"+R+"rm af rustfrit st"+R+"l": "Classic design - 90 cm wide stainless steel canopy",
 "Recirkulation m. Active AirClean- eller Longlife AirClean-filter": "Recirculation with Active AirClean or Longlife AirClean filter",
 "Til en bedst mulig funktion "+R+" ventilationsteknisk tilbeh"+R+"r": "For the best possible performance - ventilation accessories",
 "Elegant design - 60 cm bred obsidiansort emsk"+R+"rm": "Elegant design - 60 cm wide obsidian black canopy",
 "Elegant design - 90 cm bred obsidiansort emsk"+R+"rm": "Elegant design - 90 cm wide obsidian black canopy",
 "Flad 60 cm bred emsk"+R+"rm af rustfrit st"+R+"l": "Flat 60 cm wide stainless steel canopy",
 "Flad 90 cm bred emsk"+R+"rm af rustfrit st"+R+"l": "Flat 90 cm wide stainless steel canopy",
 "Tidl"+R+"st design - emsk"+R+"rm i rustfrit st"+R+"l med en bredde p"+R+" 598 mm": "Timeless design - stainless steel canopy with a width of 598 mm",
 "Tidl"+R+"st design - emsk"+R+"rm i rustfrit st"+R+"l med en bredde p"+R+" 898 mm": "Timeless design - stainless steel canopy with a width of 898 mm",
 "Pladsbesparende "+R+" passer til smalle overskabe fra 30 cm dybde": "Space-saving - fits narrow wall units from 30 cm deep",
 "Behagelig lydsvag - Miele Silence-pakke": "Pleasantly quiet - Miele Silence package",
 "Behagelig lydsvag "+R+" Miele Silence-pakke": "Pleasantly quiet - Miele Silence package",
 "Ensartet design - tilpasset Miele indbygningsprodukter": "Consistent design - matched to Miele built-in products",
 "Energieffektiv og ensartet belysning "+R+" 2 LED-p"+R+"rer": "Energy-efficient and uniform lighting - 2 LED lamps",
 "Energieffektiv og ensartet belysning "+R+" 3 LED-p"+R+"rer": "Energy-efficient and uniform lighting - 3 LED lamps",
 "Energieffektiv og ensartet belysning "+R+" 4 LED-p"+R+"rer": "Energy-efficient and uniform lighting - 4 LED lamps",
 "Lige linjer med bredde p"+R+" 880 mm og minimal dybde p"+R+" 260 mm": "Clean lines with a width of 880 mm and a minimal depth of 260 mm",
 "Lige linjer med bredde p"+R+" 880 mm og minimal dybde p"+R+" 256 mm": "Clean lines with a width of 880 mm and a minimal depth of 256 mm",
 "Moderne Touch-betjening p"+R+" glasfront": "Modern touch control on the glass front",
 "Frontpanel, der kan klappes op - til optimal udsugning": "Front panel that folds up - for optimal extraction",
 "Display med sensorbetjening - EasySensor": "Display with sensor controls - EasySensor",
 "Display med tekst og sensorbetjening - DirectSensor S": "Display with text and sensor controls - DirectSensor S",
 "Optimal og ensartet bruning - integreret quartzgrill": "Optimal and even browning - integrated quartz grill",
 "Perfekt opt"+R+"ning og tilberedning - automatikprogrammer": "Perfect defrosting and cooking - automatic programmes",
 "Retten holdes klar til servering - varmholdningsautomatik": "Keeps the dish ready to serve - automatic keep-warm function",
 "Perfekt og energibesparende - LED-belysning": "Perfect and energy-saving - LED lighting",
 "Kompakt - 26 l stort ovnrum": "Compact - 26 l oven capacity",
 "Flere forl"+R+"b i samme trin med memoryfunktion": "Several sequences in one step with memory function",
 "Mikrob"+R+"lger med s"+R+"rlig stor ydeevne - 900 W": "Microwaves with particularly high power - 900 W",
 "Meget plads - 46 l ovnrumskapacitet og 40 cm drejetallerken": "Plenty of space - 46 l oven capacity and a 40 cm turntable",
 "Til m"+R+"let med et tryk p"+R+" en knap - Popcorn": "There at the press of a button - Popcorn",
 "Hurtigt og intuitivt: Effekttrin kan vaelges, og tiden kan indstilles med en talraekke for hver kogezone.": "Fast and intuitive: power levels can be selected and the time set with a number row for each cooking zone.",
 "Med hurtig opvarmning og masser af plads tilpasser PowerFlex-kogezonen sig til dine behov.": "With fast heat-up and plenty of space, the PowerFlex cooking zone adapts to your needs.",
 "Stor og kraftig - dual-wok-braender med op til 4.500 W effekt.": "Large and powerful - dual-wok burner with up to 4,500 W output.",
}

def tr_text(s):
    if s is None: return None
    if s == "": return ""
    out = s
    for dk in sorted(PH, key=len, reverse=True):
        out = out.replace(dk, PH[dk])
    return out

result = {}
leftover = []
DK = re.compile(r'\b(med|og|til|paa|kogezone|kogeplade|emh|belysning|rustfrit|staal|bredde|reng|hurtig|nem|effektiv|opvarmning|udsyn|betjening|kogeomraade|talraekke|emsk|takket|vaere|inkl|fritst|mikrob|drejetallerken|ovnrum)\b', re.I)
for it in src:
    sub = tr_subtitle(it['subtitle'], it['cat'])
    head = tr_text(it['headline']); desc = tr_text(it['description'])
    feats = [tr_text(f) for f in (it['features'] or [])]
    result[it['id']] = {'subtitle':sub,'headline':head,'description':desc,'features':feats}
    blob = ' '.join(x for x in [sub,head,desc] if x) + ' ' + ' '.join(feats)
    if DK.search(blob) or R in blob: leftover.append((it['name'], blob))
json.dump(result, open('translated.json','w'), ensure_ascii=False, indent=1)
print("translated", len(result), "| leftover", len(leftover))
for n,b in leftover: print("==",n,"\n  ",b[:400])
