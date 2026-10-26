"""Original, deterministic synthesized first-pass audio. No third-party samples."""
from pathlib import Path
import json
import subprocess
import numpy as np
from scipy.signal import butter, sosfilt
from scipy.io.wavfile import write

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/Audio"
OUT.mkdir(parents=True, exist_ok=True)
RATE = 44100
RNG = np.random.default_rng(26026)
records = []

def hz(midi):
    return 440 * 2 ** ((midi - 69) / 12)

def pluck(midi, duration=1.2):
    t = np.arange(int(RATE * duration)) / RATE
    f = hz(midi)
    y = np.zeros_like(t)
    for h in range(1, 9):
        y += np.sin(2*np.pi*f*h*t + .12*h) * np.exp(-t*(2.5+h*.8)) / h**1.5
    return y * np.minimum(t/.004, 1)

def flute(midi, duration):
    t = np.arange(int(RATE * (duration+.15))) / RATE
    vibrato = .003*np.sin(2*np.pi*5*t)*np.minimum(t/.25,1)
    phase = 2*np.pi*hz(midi)*np.cumsum(1+vibrato)/RATE
    tone = np.sin(phase)+.17*np.sin(2*phase)+.05*np.sin(3*phase)
    envelope = np.minimum(t/.05,1)*np.minimum(np.maximum(duration+.15-t,0)/.15,1)
    return tone*envelope*.45

def noise(duration, low=200, high=7000):
    n=RNG.normal(0,1,int(RATE*duration))
    return sosfilt(butter(3,[low,high],btype="bandpass",fs=RATE,output="sos"),n)

def drum(duration=.25, strong=True):
    t=np.arange(int(RATE*duration))/RATE
    y=np.sin(2*np.pi*(65*t+8*(1-np.exp(-t*25))))*np.exp(-t*18)
    return y*.7 + noise(duration,200,3500)*np.exp(-t*35)*(.25 if strong else .08)

def add(mix, y, seconds, gain=1, pan=0, loop=False):
    start=int(round(seconds*RATE))
    stereo=np.column_stack((y*np.sqrt((1-pan)/2),y*np.sqrt((1+pan)/2)))*gain
    if loop:
        indices=(np.arange(len(y))+start)%len(mix)
        np.add.at(mix,indices,stereo)
    else:
        end=min(start+len(y),len(mix))
        if end>start: mix[start:end]+=stereo[:end-start]

def save_sfx(name, mix):
    mono=mix.mean(axis=1) if mix.ndim==2 else mix
    fade=min(441,len(mono)//5)
    mono[:fade]*=np.linspace(0,1,fade)
    mono[-fade:]*=np.linspace(1,0,fade)
    peak=float(np.max(np.abs(mono)))
    mono=mono/max(peak,1e-6)*.63
    path=OUT/(name+".wav")
    write(path,RATE,(mono*32767).astype(np.int16))
    records.append(dict(name=path.name,seconds=round(len(mono)/RATE,3),channels=1,peak=float(np.max(np.abs(mono)))))

def sfx():
    y=noise(.28,1000,9000)
    y*=np.sin(np.linspace(0,np.pi,len(y)))**2*.7
    y[:len(pluck(74,.12))]+=pluck(74,.12)*.15
    save_sfx("sfx_card_play",y)
    t=np.arange(int(RATE*.32))/RATE
    save_sfx("sfx_hit",drum(.32)+noise(.32,350,6500)*np.exp(-t*24)*.7)
    for name,notes,spacing in [
        ("sfx_magic",[74,81,86,93],.09),
        ("sfx_heal",[72,76,79,84],.13),
        ("sfx_buff",[67,74,79,83],.1),
        ("sfx_chain",[74,81,86,90],.075),
        ("sfx_victory",[62,66,69,74,78,81],.15),
        ("sfx_defeat",[69,65,62,57],.23)]:
        duration=len(notes)*spacing+1.15
        mix=np.zeros((int(duration*RATE),2))
        for i,note in enumerate(notes):
            add(mix,pluck(note,1.1),i*spacing,.65)
        if name=="sfx_chain":
            for offset in (0,.07):
                z=noise(.08,1800,10000)*np.exp(-np.arange(int(RATE*.08))/RATE*70)
                add(mix,z,offset,.6)
        save_sfx(name,mix)
    t=np.arange(int(.38*RATE))/RATE
    y=noise(.38,2000,12000)*np.exp(-t*30)+np.sin(2*np.pi*1800*t)*np.exp(-t*35)*.35
    save_sfx("sfx_critical",y)
    t=np.arange(int(1.1*RATE))/RATE
    y=noise(1.1,60,6500)*np.exp(-t*5)
    for offset in (0,.08,.16,.31):
        start=int(offset*RATE); q=drum(.18)
        y[start:start+len(q)]+=q*.5
    save_sfx("sfx_part_break",y)
    t=np.arange(int(.12*RATE))/RATE
    y=np.sin(2*np.pi*580*t)*np.exp(-t*65)+noise(.12,200,2000)*np.exp(-t*90)*.2
    save_sfx("sfx_button",y)

def music(name,bpm,roots,mode):
    beat=60/bpm
    bars=16
    length=bars*4*beat
    mix=np.zeros((int(round(length*RATE)),2))
    major=mode=="lobby"
    motif=([0,4,7,9,7,4,2,4, 7,9,11,9,7,4,2,0] if major else
           [0,3,7,10,7,5,3,2, 0,3,5,7,10,7,5,2])
    for bar in range(bars):
        root=roots[bar%len(roots)]
        third = 4 if major or root in (46,53,48,45) else 3
        chord=[root,root+third,root+7]
        # Quiet repeating lute pattern and a lower drone/bass; circular scheduling keeps tails seamless.
        for step in range(8):
            note=chord[step%3]+12
            add(mix,pluck(note,1.1),bar*4*beat+step*.5*beat,.22 if major else .19,-.35,True)
        for step in (0,2):
            add(mix,pluck(root-12,1.8),bar*4*beat+step*beat,.28,.1,True)
        for step in range(4):
            pos=bar*4*beat+step*beat
            if mode!="lobby" or step in (0,2):
                add(mix,drum(.23,step%2==0),pos,.13 if major else (.25 if step%2==0 else .12),0,True)
            if mode=="boss":
                q=noise(.065,3000,10000)*np.exp(-np.arange(int(RATE*.065))/RATE*65)
                add(mix,q,pos+.5*beat,.08,.25,True)
        # A 4-bar composed theme with answering phrases and octave variation.
        for step in range(4):
            degree=motif[(bar%4)*4+step]
            note=(62 if not major else 62)+degree+(12 if bar>=8 and mode=="boss" else 0)
            duration=beat*(.8 if step%2==0 else .65)
            if bar%8==7 and step==3: continue
            add(mix,flute(note,duration),bar*4*beat+step*beat,.18 if major else .16,.35,True)
        if mode=="boss" and bar%4==0:
            for note in chord:
                add(mix,flute(note,.8),bar*4*beat,.075,-.1,True)
    # Subtle wraparound early reflections instead of an audible ending/fade.
    dry=mix.copy()
    for delay,gain in ((.071,.13),(.139,.09),(.223,.05)):
        mix+=np.roll(dry,int(delay*RATE),axis=0)*gain
    peak=np.max(np.abs(mix))
    mix=mix/max(peak,1e-6)*.63
    wav=ROOT/"Tools/ArtPipeline"/(name+".tmp.wav")
    write(wav,RATE,(mix*32767).astype(np.int16))
    output=OUT/(name+".ogg")
    subprocess.run(["ffmpeg","-hide_banner","-loglevel","error","-y","-i",str(wav),"-c:a","libvorbis","-q:a","5",str(output)],check=True)
    wav.unlink()
    records.append(dict(name=output.name,seconds=round(len(mix)/RATE,3),channels=2,bpm=bpm,bars=bars,loop=True,peak=float(np.max(np.abs(mix)))))

if __name__=="__main__":
    sfx()
    music("bgm_battle",112,[50,46,53,48],"battle")
    music("bgm_boss",128,[50,46,43,45],"boss")
    music("bgm_lobby",90,[50,55,57,50],"lobby")
    report=ROOT/"Docs/Art/guild_audio_report.json"
    report.write_text(json.dumps({"method":"Original procedural synthesis; no external samples","sample_rate":RATE,"assets":records},indent=2),encoding="utf-8")
    print(f"Created {len(records)} original synthesized audio assets.")

