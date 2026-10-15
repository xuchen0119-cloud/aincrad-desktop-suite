from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import math, struct, json, io

ROOT = Path(__file__).resolve().parents[2] / 'cursors' / 'silverwing'
ASSETS = ROOT / 'cursors'
ASSETS.mkdir(parents=True, exist_ok=True)
SIZES = (32, 48, 64, 96, 128)
NAVY, SILVER, CYAN, WHITE = '#081426', '#D8E8F0', '#83DEF2', '#F4FAFE'

class Vector:
    def __init__(self):
        self.im = Image.new('RGBA', (256, 256))
        self.d = ImageDraw.Draw(self.im)
        self.svg = []
    def polygon(self, pts, fill=SILVER, stroke=NAVY, width=2.8):
        xy=[(round(x*4),round(y*4)) for x,y in pts]
        self.d.polygon(xy, fill=fill)
        if stroke and width:
            self.d.line(xy+[xy[0]], fill=stroke, width=round(width*4), joint='curve')
        self.svg.append('<polygon points="%s" fill="%s" stroke="%s" stroke-width="%s" stroke-linejoin="round"/>' % (' '.join(f'{x},{y}' for x,y in pts), fill, stroke or 'none', width))
    def line(self, pts, color=SILVER, width=2.2):
        xy=[(round(x*4),round(y*4)) for x,y in pts]
        self.d.line(xy, fill=color, width=round(width*4), joint='curve')
        r=width*2
        for x,y in (xy[0],xy[-1]): self.d.ellipse((x-r,y-r,x+r,y+r), fill=color)
        self.svg.append('<polyline points="%s" fill="none" stroke="%s" stroke-width="%s" stroke-linecap="round" stroke-linejoin="round"/>' % (' '.join(f'{x},{y}' for x,y in pts),color,width))
    def ellipse(self, bounds, fill=None, outline=NAVY, width=2.5):
        self.d.ellipse(tuple(round(v*4) for v in bounds), fill=fill, outline=outline, width=round(width*4))
        x0,y0,x1,y1=bounds
        self.svg.append(f'<ellipse cx="{(x0+x1)/2}" cy="{(y0+y1)/2}" rx="{(x1-x0)/2}" ry="{(y1-y0)/2}" fill="{fill or "none"}" stroke="{outline or "none"}" stroke-width="{width}"/>')
    def arc(self, center, radius, a0, a1, color, width):
        pts=[(center[0]+radius*math.cos(math.radians(a0+(a1-a0)*i/20)),center[1]+radius*math.sin(math.radians(a0+(a1-a0)*i/20))) for i in range(21)]
        self.line(pts,color,width)
    def render(self, size): return self.im.resize((size,size),Image.Resampling.LANCZOS)
    def source(self): return '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64">'+''.join(self.svg)+'</svg>'

def arrow(v, scale=1):
    def p(pts): return [(8+(x-8)*scale,6+(y-6)*scale) for x,y in pts]
    v.polygon(p([(8,6),(12,49),(23,38),(34,54),(42,49),(31,32),(46,29)]))
    v.polygon(p([(11,13),(14,43),(22,34),(36,29)]),WHITE,None,0)
    v.line(p([(12,17),(15,39),(23,32)]),CYAN,1.7)
    v.line(p([(26,37),(36,49)]),'#8AB4CB',1.8)

def ring(v, frame, center=(32,32), radius=17):
    for i in range(12):
        a=i*30-90
        dist=(i-frame)%12
        col=CYAN if dist==0 else '#D8E8F0' if dist==1 else '#618AA7' if dist==2 else '#26455F'
        v.arc(center,radius,a+3,a+20,NAVY,5.5)
        v.arc(center,radius,a+3,a+20,col,2.8)
    v.ellipse((center[0]-4,center[1]-4,center[0]+4,center[1]+4),None,'#A6C9DD',1.2)

def turn(pts,angle):
    c,s=math.cos(math.radians(angle)),math.sin(math.radians(angle))
    return [(32+(x-32)*c-(y-32)*s,32+(x-32)*s+(y-32)*c) for x,y in pts]

def resize(v,angle):
    v.line(turn([(32,15),(32,49)],angle),NAVY,6)
    v.line(turn([(32,17),(32,47)],angle),SILVER,2.6)
    v.polygon(turn([(32,8),(23,20),(41,20)],angle),SILVER,NAVY,2.8)
    v.polygon(turn([(32,56),(23,44),(41,44)],angle),SILVER,NAVY,2.8)
    v.line(turn([(29,18),(32,14),(35,18)],angle),CYAN,1.2)

def design(role,frame=0):
    v=Vector()
    if role in ('Arrow','Help','AppStarting','Pin','Person'):
        arrow(v,.81 if role in ('AppStarting','Help','Pin','Person') else 1)
        if role=='AppStarting': ring(v,frame,(46,45),10)
        elif role=='Help':
            v.line([(40,15),(43,12),(49,12),(52,15),(52,19),(46,24),(46,29)],NAVY,6)
            v.line([(40,15),(43,12),(49,12),(52,15),(52,19),(46,24),(46,29)],CYAN,2.5)
            v.ellipse((43.5,34,48.5,39),CYAN,NAVY,1.6)
        elif role=='Pin':
            v.polygon([(45,33),(39,39),(39,44),(45,55),(51,44),(51,39)],CYAN,NAVY,2)
            v.ellipse((42,37,48,43),NAVY,NAVY,1)
        elif role=='Person':
            v.ellipse((41,32,51,42),SILVER,NAVY,2)
            v.polygon([(46,43),(39,47),(38,55),(54,55),(53,47)],CYAN,NAVY,2)
        return v,(8,6)
    if role=='Wait': ring(v,frame); return v,(32,32)
    if role=='IBeam':
        for pts in [[(25,10),(39,10)],[(32,10),(32,54)],[(25,54),(39,54)]]:
            v.line(pts,NAVY,5.8);v.line(pts,SILVER,2.3)
        v.line([(32,24),(32,40)],CYAN,1.3)
    elif role=='Crosshair':
        for pts in [[(32,10),(32,26)],[(32,38),(32,54)],[(10,32),(26,32)],[(38,32),(54,32)]]:
            v.line(pts,NAVY,5);v.line(pts,SILVER,1.8)
        v.ellipse((29,29,35,35),None,CYAN,1.3)
    elif role=='NWPen':
        v.polygon([(12,53),(17,37),(41,9),(50,17),(27,45)],SILVER,NAVY,2.6)
        v.polygon([(13,51),(17,39),(24,45)],CYAN,NAVY,1.5)
        v.line([(23,37),(42,15)],WHITE,2)
        return v,(12,53)
    elif role=='No':
        v.ellipse((12,12,52,52),None,NAVY,7)
        v.ellipse((12,12,52,52),None,'#EAAEB2',3.5)
        v.line([(19,19),(45,45)],NAVY,7)
        v.line([(19,19),(45,45)],'#EAAEB2',3.5)
    elif role.startswith('Size'):
        if role=='SizeAll': resize(v,0);resize(v,90);v.ellipse((28,28,36,36),CYAN,NAVY,1.8)
        else: resize(v,dict(SizeNS=0,SizeWE=90,SizeNWSE=-45,SizeNESW=45)[role])
    elif role=='UpArrow':
        v.polygon([(32,8),(17,27),(26,27),(26,53),(38,53),(38,27),(47,27)],SILVER,NAVY,2.8)
        v.line([(32,17),(32,43)],CYAN,2)
        return v,(32,8)
    elif role=='Hand':
        v.polygon([(20,31),(20,13),(21,9),(24,7),(27,8),(29,11),(29,25),(33,23),(37,25),(40,24),(44,27),(47,27),(51,31),(50,44),(44,54),(25,54),(20,48),(13,37),(14,33),(17,32),(20,34)],SILVER,NAVY,2.6)
        v.line([(25,13),(25,31)],WHITE,2)
        v.line([(30,31),(30,40)],'#7FAAC2',1.5)
        v.line([(38,31),(38,40)],'#7FAAC2',1.5)
        v.line([(27,49),(41,49)],CYAN,2.2)
        return v,(24,9)
    return v,(32,32)

def cur(v,hotspot,sizes=SIZES):
    entries,blobs=[],[]
    offset=6+16*len(sizes)
    for size in sizes:
        im=v.render(size)
        xor=im.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes('raw','BGRA')
        stride=((size+31)//32)*4
        mask=bytearray(stride*size)
        alpha=im.getchannel('A')
        for y in range(size):
            for x in range(size):
                if alpha.getpixel((x,y))==0: mask[(size-1-y)*stride+x//8]|=128>>(x%8)
        blob=struct.pack('<IiiHHIIiiII',40,size,size*2,1,32,0,len(xor)+len(mask),0,0,0,0)+xor+mask
        hx,hy=(round(z*size/64) for z in hotspot)
        entries.append(struct.pack('<BBBBHHII',size,size,0,0,hx,hy,len(blob),offset))
        blobs.append(blob);offset+=len(blob)
    return struct.pack('<HHH',0,2,len(sizes))+b''.join(entries)+b''.join(blobs)

def chunk(tag,data): return tag+struct.pack('<I',len(data))+data+(b'\0' if len(data)%2 else b'')
def ani(role):
    frames=[]
    for f in range(12):
        v,hotspot=design(role,f)
        frames.append(chunk(b'icon',cur(v,hotspot,(32,48,64,96))))
    header=struct.pack('<9I',36,12,12,0,0,32,1,4,1)
    payload=b'ACON'+chunk(b'anih',header)+chunk(b'LIST',b'fram'+b''.join(frames))
    return b'RIFF'+struct.pack('<I',len(payload))+payload

ROLES=[('Arrow','默认指针'),('Hand','链接选择'),('IBeam','文本选择'),('Wait','忙碌 · 动态'),('AppStarting','后台运行 · 动态'),('Help','帮助选择'),('Crosshair','精确选择'),('NWPen','手写'),('No','不可用'),('SizeNS','垂直调整'),('SizeWE','水平调整'),('SizeNWSE','对角调整 1'),('SizeNESW','对角调整 2'),('SizeAll','移动'),('UpArrow','候选选择'),('Pin','位置选择'),('Person','人物选择')]
manifest={}
for role,label in ROLES:
    v,hotspot=design(role)
    ext='ani' if role in ('Wait','AppStarting') else 'cur'
    path=ASSETS/(role+'.'+ext)
    path.write_bytes(ani(role) if ext=='ani' else cur(v,hotspot))
    (ASSETS/(role+'.svg')).write_text(v.source(),encoding='utf-8')
    manifest[role]=dict(file=path.name,label=label,hotspot64=hotspot,sizes=list((32,48,64,96) if ext=='ani' else SIZES),frames=12 if ext=='ani' else 1)
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

fontpath=str(Path(__import__('os').environ.get('WINDIR', 'C:/Windows')) / 'Fonts' / 'msyh.ttc')
title=ImageFont.truetype(fontpath,34)
label_font=ImageFont.truetype(fontpath,19)
small=ImageFont.truetype(fontpath,16)
sheet=Image.new('RGB',(1360,1120),'#071426');d=ImageDraw.Draw(sheet)
d.text((40,25),'AINCRAD  /  银翼光标',font=title,fill='#EDF5FA')
d.text((42,78),'银白主体 · 冰蓝点缀 · 深色轮廓    /    深浅背景预览',font=small,fill='#9EBACA')
for i,(role,label) in enumerate(ROLES):
    x=40+(i%4)*328;y=124+(i//4)*184
    d.rounded_rectangle((x,y,x+308,y+164),radius=14,fill='#102338',outline='#284359')
    d.text((x+16,y+13),label,font=label_font,fill='#DBEAF4')
    d.rounded_rectangle((x+168,y+48,x+292,y+148),radius=8,fill='#EAF0F5')
    v,_=design(role)
    sheet.paste(v.render(64),(x+34,y+64),v.render(64))
    sheet.paste(v.render(32),(x+116,y+82),v.render(32))
    sheet.paste(v.render(48),(x+205,y+73),v.render(48))
d.text((42,1075),'17 种状态 · 多分辨率 CUR · 双动态 ANI · 保留原方案备份',font=small,fill='#9EBACA')
sheet.save(ROOT/'preview.png')
gif=[]
for f in range(12):
    canvas=Image.new('RGB',(480,180),'#071426')
    gd=ImageDraw.Draw(canvas);gd.rounded_rectangle((250,20,460,160),radius=12,fill='#EAF0F5')
    for role,x in [('Wait',40),('AppStarting',150),('Wait',280),('AppStarting',370)]:
        v,_=design(role,f);im=v.render(64);canvas.paste(im,(x,58),im)
    gif.append(canvas)
gif[0].save(ROOT/'animation-preview.gif',save_all=True,append_images=gif[1:],duration=67,loop=0)

checks=[]
for role,info in manifest.items():
    data=(ASSETS/info['file']).read_bytes()
    if data[:4]==b'RIFF':
        assert struct.unpack_from('<I',data,4)[0]==len(data)-8
        assert data[8:12]==b'ACON'
        assert data.count(b'icon')==12
    else:
        reserved,kind,count=struct.unpack_from('<HHH',data)
        assert reserved==0 and kind==2 and count==5
        for n in range(count):
            w,h,_,_,hx,hy,length,offset=struct.unpack_from('<BBBBHHII',data,6+n*16)
            assert 0<=hx<w and 0<=hy<h and offset+length<=len(data)
            assert struct.unpack_from('<I',data,offset)[0]==40
    checks.append(dict(role=role,bytes=len(data),formatValid=True))
(ROOT/'asset-checks.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(json.dumps(dict(roles=len(manifest),filesValidated=len(checks),preview=str(ROOT/'preview.png'))))
