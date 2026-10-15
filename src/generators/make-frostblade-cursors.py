"""Original vector artwork; SAO sword silhouette and restrained light-blade styling."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import ast, math, struct, json

ROOT = Path(__file__).resolve().parents[2] / 'cursors' / 'frostblade'
ASSETS = ROOT / 'cursors'
ASSETS.mkdir(parents=True, exist_ok=True)
SIZES = (32, 48, 64, 96, 128)
NAVY, SILVER, CYAN, WHITE = '#071525', '#D2E7F1', '#67D9FF', '#F4FCFF'
base = Path(__file__).with_name('make-aincrad-cursors.py').read_text(encoding='utf-8')
tree = ast.parse(base)
nodes = [n for n in tree.body if (isinstance(n, ast.ClassDef) and n.name == 'Vector') or
         (isinstance(n, ast.FunctionDef) and n.name in ('cur', 'chunk', 'turn'))]
exec(compile(ast.Module(body=nodes, type_ignores=[]), '<vector-core>', 'exec'), globals())


def stroke(v, pts, color=CYAN, width=3):
    v.line(pts, NAVY, width+3)
    v.line(pts, color, width)


def sword(v, tip=(8, 6), angle=48, scale=1):
    theta = math.radians(angle)
    dx, dy = math.cos(theta), math.sin(theta)
    px, py = -dy, dx
    def p(t, w=0):
        return (tip[0]+scale*(dx*t+px*w), tip[1]+scale*(dy*t+py*w))
    def poly(coords, fill, outline=NAVY, width=1.8):
        v.polygon([p(t, w) for t, w in coords], fill, outline, width*scale)
    # A broad tapered blade reads as a sword at the Windows default 32px size.
    # The narrow translucent rim is deliberately confined to the blade edge.
    v.line([p(8), p(34)], '#67D9FF26', 12*scale)
    poly([(0, 0), (8, -4.5), (33, -4.5), (37, -2.8),
          (37, 2.8), (33, 4.5), (8, 4.5)], CYAN)
    poly([(1.7, 0), (9, -2.6), (34, -2.6), (36, 0), (9, 1.1)], WHITE, None, 0)
    poly([(9, 1.1), (36, 0), (34, 2.6), (9, 2.8)], '#3295C2', None, 0)
    # One angular guard, one grip and one pommel, without unreadable ribbing.
    poly([(36, -8), (39, -8), (41, -3), (41, 3), (39, 8),
          (36, 8), (38, 2.5), (38, -2.5)], SILVER)
    poly([(40, -2.9), (51, -2.9), (53, 0), (51, 2.9), (40, 2.9)], '#183C56')
    v.line([p(42, -0.6), p(49, -0.6)], CYAN, 1.7*scale)
    poly([(51, -3.7), (55, -2.7), (56, 0), (55, 2.7), (51, 3.7)], SILVER)


def ring(v, frame, center=(32,32), radius=19):
    # A smooth orbit around a stationary blade avoids a spinning needle.
    v.arc(center, radius, 0, 360, NAVY, 5.5)
    v.arc(center, radius, 0, 360, '#34556B', 2.6)
    start = frame*15-90
    v.arc(center, radius, start, start+100, CYAN, 3.3)
    v.arc(center, radius, start, start+22, WHITE, 2.4)


def resize(v, angle, compact=False):
    top, bottom = (10,54) if not compact else (9,55)
    half = 7 if not compact else 6
    def pts(coords): return turn(coords, angle)
    # Recognizable arrow tips share the blade's cyan facets and silver guard.
    v.polygon(pts([(32,top),(32-half,top+12),(29,top+12),
                   (29,bottom-12),(32-half,bottom-12),(32,bottom),
                   (32+half,bottom-12),(35,bottom-12),(35,top+12),
                   (32+half,top+12)]), CYAN, NAVY, 2)
    v.line(pts([(32,top+7),(32,bottom-7)]), WHITE, 1.6)
    v.polygon(pts([(27,29),(37,29),(37,35),(27,35)]), SILVER, NAVY, 1.8)
    v.line(pts([(30,32),(34,32)]), '#31586F', 1.4)


def design(role, frame=0):
    v = Vector()
    if role in ('Arrow','Help','AppStarting','Hand','Pin','Person'):
        sword(v, scale=1 if role=='Arrow' else .9)
        if role=='AppStarting':
            ring(v, frame, (48,46), 9)
        elif role=='Help':
            stroke(v, [(43,13),(46,10),(51,10),(54,13),(54,18),(49,22),(49,26)], WHITE, 2.8)
            v.ellipse((47.3,31,50.7,34.4), WHITE, NAVY, 1.4)
        elif role=='Hand':
            # Link rings are larger and simpler than the previous tiny chain.
            for bounds, color in [((40,10,50,20),WHITE),((46,16,56,26),CYAN)]:
                v.ellipse(bounds, None, NAVY, 5.5)
                v.ellipse(bounds, None, color, 2.7)
        elif role=='Pin':
            v.polygon([(48,30),(40,35),(40,42),(48,53),(56,42),(56,35)], CYAN, NAVY, 2)
            v.ellipse((45,34,51,40), NAVY, None, 0)
        elif role=='Person':
            v.ellipse((44,30,54,40), SILVER, NAVY, 2)
            v.polygon([(49,42),(42,46),(41,53),(57,53),(56,46)], CYAN, NAVY, 2)
        return v, (8,6)
    if role=='Wait':
        ring(v, frame, radius=23)
        sword(v, (32,16), 90, .55)
    elif role=='IBeam':
        # Symmetrical I-beam, no handle below the insertion location.
        stroke(v, [(32,11),(32,53)], CYAN, 2.6)
        for y in (10,54):
            stroke(v, [(25,y),(39,y)], SILVER, 2.6)
        v.line([(32,17),(32,47)], WHITE, 1.2)
    elif role=='Crosshair':
        for a,b in [((32,10),(32,24)),((32,40),(32,54)),
                    ((10,32),(24,32)),((40,32),(54,32))]:
            stroke(v, [a,b], CYAN, 2.5)
        v.ellipse((29,29,35,35), None, NAVY, 3)
        v.ellipse((29,29,35,35), None, WHITE, 1.4)
    elif role=='NWPen':
        sword(v, (10,54), -48, .94)
        return v, (10,54)
    elif role=='No':
        v.ellipse((11,11,53,53), None, NAVY, 7)
        v.ellipse((11,11,53,53), None, '#F6A3AE', 3.8)
        stroke(v, [(18,18),(46,46)], '#F6A3AE', 4)
    elif role.startswith('Size'):
        if role=='SizeAll':
            resize(v,0,True); resize(v,90,True)
            v.ellipse((28,28,36,36), SILVER, NAVY, 1.8)
        else:
            resize(v, dict(SizeNS=0,SizeWE=90,SizeNWSE=-45,SizeNESW=45)[role])
    elif role=='UpArrow':
        sword(v, (32,5), 90, .94)
        return v, (32,5)
    return v, (32,32)


def ani(role):
    frames=[]
    for f in range(24):
        v,hotspot=design(role,f)
        frames.append(chunk(b'icon',cur(v,hotspot,(32,48,64,96))))
    header=struct.pack('<9I',36,24,24,0,0,32,1,3,1)
    payload=b'ACON'+chunk(b'anih',header)+chunk(b'LIST',b'fram'+b''.join(frames))
    return b'RIFF'+struct.pack('<I',len(payload))+payload


ROLES=[('Arrow','默认指针'),('Hand','链接选择'),('IBeam','文本选择'),
       ('Wait','忙碌 · 动态'),('AppStarting','后台运行 · 动态'),('Help','帮助选择'),
       ('Crosshair','精确选择'),('NWPen','手写'),('No','不可用'),('SizeNS','垂直调整'),
       ('SizeWE','水平调整'),('SizeNWSE','对角调整 1'),('SizeNESW','对角调整 2'),
       ('SizeAll','移动'),('UpArrow','候选选择'),('Pin','位置选择'),('Person','人物选择')]
manifest={}
for role,label in ROLES:
    v,hotspot=design(role)
    ext='ani' if role in ('Wait','AppStarting') else 'cur'
    path=ASSETS/(role+'.'+ext)
    path.write_bytes(ani(role) if ext=='ani' else cur(v,hotspot))
    (ASSETS/(role+'.svg')).write_text(v.source(),encoding='utf-8')
    manifest[role]=dict(file=path.name,label=label,hotspot64=hotspot,
                      sizes=list((32,48,64,96) if ext=='ani' else SIZES),
                      frames=24 if ext=='ani' else 1)
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

fontpath=str(Path(__import__('os').environ.get('WINDIR', 'C:/Windows')) / 'Fonts' / 'msyh.ttc')
title=ImageFont.truetype(fontpath,32)
label_font=ImageFont.truetype(fontpath,19)
small=ImageFont.truetype(fontpath,16)
sheet=Image.new('RGB',(1360,1120),'#071426');d=ImageDraw.Draw(sheet)
d.text((40,25),'AINCRAD / 冰蓝剑光',font=title,fill=WHITE)
d.text((42,78),'宽刃轮廓 · 简洁护手 · 克制光晕    /    64、32、48 像素深浅背景预览',font=small,fill='#9EBACA')
for i,(role,label) in enumerate(ROLES):
    x=40+(i%4)*328; y=124+(i//4)*184
    d.rounded_rectangle((x,y,x+308,y+164),radius=14,fill='#102338',outline='#284359')
    d.text((x+16,y+13),label,font=label_font,fill='#DBEAF4')
    d.rounded_rectangle((x+168,y+48,x+292,y+148),radius=8,fill='#EAF0F5')
    v,_=design(role)
    for size,offset in [(64,(34,64)),(32,(116,82)),(48,(205,73))]:
        im=v.render(size);sheet.paste(im,(x+offset[0],y+offset[1]),im)
d.text((42,1075),'17 种状态 · 多分辨率 CUR · 双动态 ANI · 保留原方案备份',font=small,fill='#9EBACA')
sheet.save(ROOT/'preview.png')

gif=[]
for f in range(24):
    canvas=Image.new('RGB',(480,180),'#071426');gd=ImageDraw.Draw(canvas)
    gd.rounded_rectangle((250,20,460,160),radius=12,fill='#EAF0F5')
    for role,x in [('Wait',40),('AppStarting',150),('Wait',280),('AppStarting',370)]:
        v,_=design(role,f);im=v.render(64);canvas.paste(im,(x,58),im)
    gif.append(canvas)
gif[0].save(ROOT/'animation-preview.gif',save_all=True,append_images=gif[1:],duration=50,loop=0)

# Compare true 32px renderings with the previous source art, on both backgrounds.
# Separate function globals keep the previous generator from replacing this design.
oldns=dict(globals())
exec(compile(Path(__file__).with_name('make-lightsaber-cursors.py').read_text(encoding='utf-8').split("export=base[")[0],'<previous-design>','exec'),oldns)
comparison=Image.new('RGB',(920,380),'#071426');cd=ImageDraw.Draw(comparison)
cd.text((28,20),'32 px 实际尺寸对照',font=title,fill=WHITE)
for row,(name,fn) in enumerate([('上一版',oldns['design']),('冰蓝剑光',design)]):
    y=105+row*130
    cd.text((28,y+20),name,font=label_font,fill=SILVER)
    for col,role in enumerate(('Arrow','Hand','IBeam','SizeNS','Wait')):
        x=170+col*144
        cd.rounded_rectangle((x+57,y,x+120,y+75),radius=7,fill='#EAF0F5')
        v,_=fn(role)
        for xx in (x+10,x+73):
            im=v.render(32);comparison.paste(im,(xx,y+21),im)
        if row==0:cd.text((x,y-25),role,font=small,fill='#9EBACA')
comparison.save(ROOT/'comparison.png')

checks=[]
for role,info in manifest.items():
    data=(ASSETS/info['file']).read_bytes()
    if data[:4]==b'RIFF':
        assert struct.unpack_from('<I',data,4)[0]==len(data)-8
        assert data[8:12]==b'ACON' and data.count(b'icon')==24
    else:
        reserved,kind,count=struct.unpack_from('<HHH',data)
        assert reserved==0 and kind==2 and count==5
        for n in range(count):
            w,h,_,_,hx,hy,length,offset=struct.unpack_from('<BBBBHHII',data,6+n*16)
            assert 0<=hx<w and 0<=hy<h and offset+length<=len(data)
            assert struct.unpack_from('<I',data,offset)[0]==40
    for size in info['sizes']:
        v,hotspot=design(role)
        im=v.render(size)
        assert im.getbbox() is not None
        # A nonempty margin prevents blade/ring clipping at any exported resolution.
        # Ignore faint Lanczos ringing; visible pixels must stay inside the canvas.
        visible=im.getchannel('A').point(lambda a:255 if a>=16 else 0)
        assert not visible.crop((0,0,size,1)).getbbox(), (role,size,'top clipping')
        assert not visible.crop((0,size-1,size,size)).getbbox(), (role,size,'bottom clipping')
        assert not visible.crop((0,0,1,size)).getbbox(), (role,size,'left clipping')
        assert not visible.crop((size-1,0,size,size)).getbbox(), (role,size,'right clipping')
    checks.append(dict(role=role,bytes=len(data),formatValid=True))
(ROOT/'asset-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(json.dumps(dict(roles=len(manifest),filesValidated=len(checks),preview=str(ROOT/'preview.png'))))
