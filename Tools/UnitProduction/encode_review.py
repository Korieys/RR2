"""Encode real Unity frames at their measured timestamps, without generated motion."""
import bpy,csv,struct,sys
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];root=Path(args[0]);out=root/'videos';out.mkdir(exist_ok=True)
for timeline in sorted((root/'clips').glob('*/timings.csv')):
    name=timeline.parent.name
    if len(args)>1 and name not in args[1:]:continue
    rows=list(csv.DictReader(timeline.open()))
    if len(rows)<2:continue
    bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;edit=scene.sequence_editor_create()
    scene.render.resolution_x,scene.render.resolution_y=struct.unpack('>II',(timeline.parent/rows[0]['frame']).read_bytes()[16:24])
    scene.render.resolution_percentage=100;scene.render.fps=30;scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    scene.render.image_settings.media_type='VIDEO';scene.render.image_settings.file_format='FFMPEG'
    scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264';scene.render.ffmpeg.constant_rate_factor='HIGH';scene.render.ffmpeg.ffmpeg_preset='GOOD';scene.render.filepath=str(out/(name+'.mp4'))
    zero=float(rows[0]['seconds']);samples={max(1,round((float(r['seconds'])-zero)*30)+1):r['frame'] for r in rows};starts=sorted(samples);end=starts[-1]+3
    for i,start in enumerate(starts):
        stop=starts[i+1] if i+1<len(starts) else end
        strip=edit.strips.new_image(samples[start],str(timeline.parent/samples[start]),channel=1,frame_start=start);strip.frame_final_duration=stop-start
    scene.frame_start=1;scene.frame_end=end-1;scene.render.use_sequencer=True;scene.render.use_compositing=False
    bpy.ops.render.render(animation=True);print('ENCODED',name,len(rows),'actual frames',round((end-1)/30,2),'seconds',flush=True)
