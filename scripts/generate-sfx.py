from pathlib import Path
import wave,math,random,struct
out=Path('public/audio/sfx');out.mkdir(parents=True,exist_ok=True)
rate=24000;rng=random.Random(808)
def write(name,data):
 peak=max(abs(x) for x in data) or 1
 with wave.open(str(out/(name+'.wav')),'w') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes(b''.join(struct.pack('<h',int(max(-1,min(1,x/peak*.85))*32767)) for x in data))
def shot(duration,bass,decay,bright,variant):
 data=[];low=0;phase=0
 for i in range(int(rate*duration)):
  t=i/rate;noise=rng.uniform(-1,1);low+=.12*(noise-low);phase+=2*math.pi*(bass*math.exp(-t*3))/rate
  body=(low*1.8+math.sin(phase)*.28)*math.exp(-t*decay);crack=(noise-low)*bright*math.exp(-t*85);mechanical=.14*rng.uniform(-1,1)*math.exp(-abs(t-.045)*120);tail=low*.35*math.exp(-t*6)
  data.append((body+crack+mechanical+tail)*min(1,t/.001))
 return data
profiles={'carbine':(.46,84,15,.8),'marksman':(.62,69,11,.95),'smg':(.29,115,20,.6),'lmg':(.5,77,12,.78),'battleRifle':(.58,67,11,.9),'sniper':(.85,54,9,1.1),'pistol':(.37,100,17,.65),'cannon':(.92,39,6,1.1),'explosion':(1.3,31,3,.65)}
for name,(duration,bass,decay,bright) in profiles.items():write(name,shot(duration,bass,decay,bright,0))
for name,dur in [('foot-gravel',.19),('foot-road',.14),('near-miss',.24),('impact',.2),('cloth',.35),('land',.3)]:
 data=[];low=0
 for i in range(int(rate*dur)):
  t=i/rate;n=rng.uniform(-1,1);low+=.2*(n-low);env=math.sin(math.pi*t/dur)**2 if name in ['near-miss','cloth'] else math.exp(-t*25)*min(1,t/.003);data.append((n-low if name=='near-miss' else low if name in ['land','foot-road'] else n*.3+low)*env)
 write(name,data)
data=[];low=0
for i in range(rate*12):
 t=i/rate;low+=.003*(rng.uniform(-1,1)-low);env=.55+.2*math.sin(t*.7)+.12*math.sin(t*1.4);data.append(low*env)
write('wind',data)

print('Generated procedural SFX. Existing CC0 UI and reload assets are retained; see docs/licenses/Audio-Credits.md.')
