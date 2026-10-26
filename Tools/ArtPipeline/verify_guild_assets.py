"""Read-only delivery audit: dimensions, alpha, animation cells and audio decoding."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
import soundfile as sf

root=Path(__file__).resolve().parents[2]
art=root/"Assets/Resources/Art"
manifest=json.loads((root/"Docs/Art/guild_assets_manifest.json").read_text(encoding="utf-8-sig"))
expected={x["folder"]+"/"+x["name"]+".png":x for x in manifest["assets"]}
expected["Cards/icon_star.png"]=dict(width=32,height=32,transparent=True)
expected["Cards/card_awaken_glow.png"]=dict(width=300,height=420,transparent=True)
report=dict(graphics=0,audio=0,errors=[],walk_frames={},audio_metrics=[],alpha_metrics={})
for key,entry in expected.items():
    path=art/key
    if not path.exists():
        report["errors"].append("Missing "+key);continue
    with Image.open(path) as im:
        if im.size!=(entry["width"],entry["height"]): report["errors"].append("Size "+key)
        rgba=np.array(im.convert("RGBA"))
    alpha=rgba[:,:,3]
    report["alpha_metrics"][key]=dict(transparent_ratio=round(float(np.mean(alpha==0)),4),max_alpha=int(alpha.max()))
    if entry["transparent"] and not np.any(alpha==0): report["errors"].append("No transparent pixels "+key)
    if not entry["transparent"] and np.any(alpha<255): report["errors"].append("Background not opaque "+key)
    if entry.get("kind")=="animation":
        frames=[]
        for i in range(4):
            cell=rgba[:,128*i:128*(i+1)]
            ys,xs=np.where(cell[:,:,3]>100)
            if len(xs)==0: report["errors"].append("Empty walk frame "+key+str(i));continue
            frames.append(dict(index=i,bounds=[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())]))
        report["walk_frames"][key]=frames
    report["graphics"]+=1
audio=root/"Assets/Resources/Audio"
for path in sorted(audio.iterdir()):
    if path.suffix not in (".wav",".ogg"):continue
    samples,sr=sf.read(path,always_2d=True)
    peak=float(np.max(np.abs(samples)))
    if sr!=44100:report["errors"].append("Sample rate "+path.name)
    if not np.isfinite(samples).all() or peak<.001 or peak>=1:report["errors"].append("Audio levels "+path.name)
    report["audio_metrics"].append(dict(name=path.name,sample_rate=sr,channels=samples.shape[1],seconds=round(len(samples)/sr,3),peak=round(peak,4),rms=round(float(np.sqrt(np.mean(samples**2))),4)))
    report["audio"]+=1
if report["graphics"]!=75:report["errors"].append("Expected 75 graphics")
if report["audio"]!=14:report["errors"].append("Expected 14 audio assets")
output=root/"Docs/Art/guild_delivery_audit.json"
output.write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps({k:report[k] for k in ("graphics","audio","errors","walk_frames")},indent=2))
raise SystemExit(bool(report["errors"]))

