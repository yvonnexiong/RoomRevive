# -*- coding: utf-8 -*-
import urllib.request, json

API = 'http://localhost:4173'

data = {
 '014936a96413fa48': dict(  # CS 7612 FL SORT
   subtitle='SmartLine induktionsplade med PowerFlex-kogezone',
   headline='med induktionsvarmet PowerFlex-kogezone',
   description='Hurtigt og intuitivt: Effekttrin kan vaelges, og tiden kan indstilles med en talraekke for hver kogezone.',
   features=['Intuitivt, hurtigt valg via talraekker - SmartSelect','Elegant design - indbygget forsaenket i eller oven paa bordplade','Kan kombineres perfekt med alle SmartLine-elementer','Fleksibel og hurtig - 2 kogezoner inkl. 1 PowerFlex-omraade','Meget korte opkogstider - TwinBooster'],
   color='Sort'),
 'd09ee6ed4ad4d394': dict(  # KM 7164 FR
   subtitle='Induktionskogeplade GOLD',
   headline='med PowerFlex-kogeomraade og knapbetjening',
   features=['Direkte betjening - knap paa kogepladen','4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade','626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen','Meget korte opkogstider - TwinBooster'],
   color='Sort, ramme i rustfrit staal'),
 'd0e055394162f975': dict(  # KM 7363 FR
   subtitle='Induktionskogeplade SILVER',
   headline='med flex-kogeomraade til stort kogegrej',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 kogezoner, inkl. 1 flex-kogeomraade','626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen','Kommunikation med emhaetten - automatikfunktion Con@ctivity','Praktisk brug af stort kogegrej takket vaere kombinerbare Flex-kogezoner'],
   color='Sort, ramme i rustfrit staal'),
 '3d8630063d36fbda': dict(  # KM 7414 FX
   subtitle='Induktionskogeplade GOLD',
   headline='med PowerFlex-kogeomraade til maksimal effekt',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade','752 mm bred til indbygning i plan med bordpladen','Meget korte opkogstider - TwinBooster','Kommunikation med emhaetten - automatikfunktion Con@ctivity'],
   color='Sort'),
 '05e2e214ce8967a2': dict(  # KM 6520 FR
   subtitle='Glaskeramisk kogeplade SILVER',
   headline='med 4 kogezoner til stoerst mulig komfort',
   features=['Enkel betjening - EasySelect','Indbydende design - 574 mm bred med ramme hele vejen rundt','Saerdeles fleksibel - inkl. 4 kogezoner 1 vario-zone','Komfortabel - opkogsautomatik for hver enkelt kogezone','Sikker - indikator for restvarme med tre trin for hver kogezone'],
   color='Rustfrit staal'),
 '8f14ba2d9885811a': dict(  # KM 7361 FL
   subtitle='Induktionskogeplade SILVER',
   headline='med 4 individuelle kogezoner til en attraktiv pris',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 individuelle kogezoner i forskellige stoerrelser','620 mm bred til indbygning oven paa eller i plan med bordpladen','Kommunikation med emhaetten - automatikfunktion Con@ctivity'],
   color='Sort'),
 '9d6e9a5a01b238c4': dict(  # KM 7361 FR
   subtitle='Induktionskogeplade SILVER',
   headline='med 4 individuelle kogezoner til en attraktiv pris',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 individuelle kogezoner i forskellige stoerrelser','626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen','Kommunikation med emhaetten - automatikfunktion Con@ctivity'],
   color='Sort, ramme i rustfrit staal'),
 'b8a10326b4e04b09': dict(  # KM 7363 FL
   subtitle='Induktionskogeplade SILVER',
   headline='med flex-kogeomraade til stort kogegrej',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 kogezoner, inkl. 1 flex-kogeomraade','Kommunikation med emhaetten - automatikfunktion Con@ctivity','Praktisk brug af stort kogegrej takket vaere kombinerbare Flex-kogezoner'],
   color='Sort'),
 '7fd9fe5208806d6b': dict(  # KM 7373 FL
   subtitle='Induktionskogeplade SILVER',
   headline='med flex-kogeomraade til stort kogegrej',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 kogezoner, inkl. 1 flex-kogeomraade','800 mm bred til indbygning oven paa eller i plan med bordpladen','Kommunikation med emhaetten - automatikfunktion Con@ctivity','Praktisk brug af stort kogegrej takket vaere kombinerbare Flex-kogezoner'],
   color='Sort'),
 'b22175a2604c2415': dict(  # KM 8462 FL
   subtitle='Induktionskogeplade GOLD',
   headline='620 mm | Individuelle kogezoner og PowerFlex-kogeomraade',
   description='Med hurtig opvarmning og masser af plads tilpasser PowerFlex-kogezonen sig til dine behov.',
   features=['4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade','Praecise indstillinger via SingleSlide','Korteste opvarmningstid takket vaere boostertrinnet','620 mm bred til indbygning oven paa eller i plan med bordpladen'],
   color='Sort'),
 '4945c79aa3e35d67': dict(  # KM 8462 FR
   subtitle='Induktionskogeplade GOLD',
   headline='626 mm | Individuelle kogezoner og PowerFlex-kogeomraade',
   features=['4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade','Praecise indstillinger via SingleSlide','Korteste opvarmningstid takket vaere boostertrinnet','626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen'],
   color='Sort, ramme i rustfrit staal'),
 '7c02bd37257a72b4': dict(  # KM 7464 FL
   subtitle='Induktionskogeplade GOLD',
   headline='med PowerFlex-kogeomraade til maksimal effekt',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade','620 mm bred til indbygning oven paa eller i plan med bordpladen','Meget korte opkogstider - TwinBooster','Kommunikation med emhaetten - automatikfunktion Con@ctivity'],
   color='Sort'),
 '795a2b322e6204ed': dict(  # KM 7464 FR
   subtitle='Induktionskogeplade GOLD',
   headline='med PowerFlex-kogeomraade til maksimal effekt',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade','626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen','Meget korte opkogstider - TwinBooster','Kommunikation med emhaetten - automatikfunktion Con@ctivity'],
   color='Sort, ramme i rustfrit staal'),
 '7e5e5c62292ba3c6': dict(  # KM 7466 FR 125
   subtitle='Induktionskogeplade GOLD',
   headline='med PowerFlex-kogeomraade til maksimal effekt',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade','626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen','Meget korte opkogstider - TwinBooster','Netvaerkstilslutning med Con@ctivity og Miele@home'],
   color='Sort, ramme i rustfrit staal'),
 'da8c00eabc422251': dict(  # KM 8565 FR
   subtitle='Induktionskogeplade GOLD',
   headline='626 mm | PowerFlex-kogeomraader | M Sense ready',
   features=['4 kogezoner inkl. 2 effektive PowerFlex-kogeomraader','Praecis indstilling via 4 MultiSlide slidere og intelligent tilslutning','Til tilberedning med M Sense-kogegrej - M Sense ready','Korteste opvarmningstid takket vaere boostertrinnet','626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen'],
   color='Sort, ramme i rustfrit staal'),
 '4ec8424b64c5731d': dict(  # KM 8463 FX
   subtitle='Induktionskogeplade GOLD',
   headline='592 mm | Individuelle kogezoner og PowerFlex-kogeomraade',
   description='Med hurtig opvarmning og masser af plads tilpasser PowerFlex-kogezonen sig til dine behov.',
   features=['4 kogezoner inkl. 1 effektivt PowerFlex-kogeomraade','Praecise indstillinger via SingleSlide','Korteste opvarmningstid takket vaere boostertrinnet','592 mm bred til indbygning i plan med bordpladen'],
   color='Sort'),
 'dccbc72ce1f05997': dict(  # KM 8482 FL
   subtitle='Induktionskogeplade GOLD',
   headline='800 mm | Individuelle kogezoner og PowerFlex XL-kogeomraade',
   features=['4 kogezoner inkl. 1 effektivt PowerFlex XL-kogeomraade','Praecise indstillinger via SingleSlide','Korteste opvarmningstid takket vaere boostertrinnet','800 mm bred til indbygning oven paa eller i plan med bordpladen'],
   color='Sort'),
 '977714109cb2fb00': dict(  # KM 8482 FR
   subtitle='Induktionskogeplade GOLD',
   headline='806 mm | Individuelle kogezoner og PowerFlex XL-kogeomraade',
   features=['4 kogezoner inkl. 1 effektivt PowerFlex XL-kogeomraade','Praecise indstillinger via SingleSlide','Korteste opvarmningstid takket vaere boostertrinnet','806 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen'],
   color='Sort, ramme i rustfrit staal'),
 '0dab7bd21e1e4a33': dict(  # KM 7564 FR
   subtitle='Induktionskogeplade GOLD',
   headline='med 2 PowerFlex-kogeomraader til maksimal effekt',
   features=['Intuitivt, hurtigt valg via talraekker - SmartSelect','4 kogezoner inkl. 2 effektive PowerFlex-kogeomraader','626 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen','Meget korte opkogstider - TwinBooster','Kommunikation med emhaetten - automatikfunktion Con@ctivity'],
   color='Sort, ramme i rustfrit staal'),
 'b2125eafd24d8968': dict(  # CS 7101 1 FL
   subtitle='SmartLine gasblus med dual-wok-braender',
   headline='med dual-wok-braender',
   description='Stor og kraftig - dual-wok-braender med op til 4.500 W effekt.',
   features=['Nem betjening med drejeknapper - kan betjenes med en haand','Elegant design - indbygget forsaenket i eller oven paa bordplade','Kan kombineres perfekt med alle SmartLine-elementer','Nem rengoering - ComfortClean-rist til rengoering i opvaskemaskine'],
   color='Sort'),
 'ec375c5a7ae2887b': dict(  # CSDA 7001 FL
   subtitle='SmartLine integreret bordemfang PLATINUM',
   headline='integreret bordemfang til udluftning til det fri eller recirkulation',
   features=['Intuitivt, hurtigt valg via talraekker - SmartSelect','Elegant design - indbygget forsaenket i eller oven paa bordplade','Kan kombineres perfekt med alle SmartLine-elementer','Effektiv filtrering - 10-lags metalfedtfilter i rustfrit staal','Energibesparende og lydsvag - effektiv ECO-motor'],
   color='Sort'),
 '4f408e6f341b0221': dict(  # KM 8484 FL
   subtitle='Induktionskogeplade GOLD',
   headline='800 mm | Individuelle kogezoner og PowerFlex XL-kogeomraade',
   features=['5 kogezoner inkl. 1 effektivt PowerFlex XL-kogeomraade','Praecise indstillinger via SingleSlide','Korteste opvarmningstid takket vaere boostertrinnet','800 mm bred til indbygning oven paa eller i plan med bordpladen'],
   color='Sort'),
 'be2f410bfcfe4b87': dict(  # KM 7373 FR
   subtitle='Induktionskogeplade SILVER',
   headline='med flex-kogeomraade til stort kogegrej',
   features=['Intuitivt og hurtigt valg med talraekke - ComfortSelect','4 kogezoner, inkl. 1 flex-kogeomraade','806 mm bred og ramme i rustfrit staal til indbygning oven paa bordpladen','Kommunikation med emhaetten - automatikfunktion Con@ctivity','Praktisk brug af stort kogegrej takket vaere kombinerbare Flex-kogezoner'],
   color='Sort, ramme i rustfrit staal'),
}

print('items to write:', len(data))
ok = 0
for iid, fields in data.items():
    payload = dict(fields)
    payload['_copySource'] = 'https://www.miele.dk product page'
    payload['_copyQuality'] = 'product-page-verified'
    body = json.dumps({'product': payload}).encode('utf-8')
    req = urllib.request.Request(API + '/api/items/' + iid, data=body, method='PUT',
                                 headers={'Content-Type': 'application/json'})
    try:
        urllib.request.urlopen(req)
        ok += 1
    except Exception as e:
        print('FAIL', iid, e)
print('written OK:', ok)
