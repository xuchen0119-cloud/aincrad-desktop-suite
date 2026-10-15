from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import ast, math, struct, json

ROOT = Path(__file__).resolve().parents[2] / 'cursors' / 'lightsaber'
ASSETS=ROOT/'cursors'
ASSETS.mkdir(parents=True,exist_ok=True)
SIZES=(32,48,64,96,128)
NAVY,SILVER,CYAN,WHITE='#071425','#C6DCE8','#75DFFF','#F5FCFF'
base=Path(__file__).with_name('make-aincrad-cursors.py').read_text(encoding='utf-8')
tree=ast.parse(base)
reused=[n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='Vector' or isinstance(n,ast.FunctionDef) and n.name in ('cur','chunk','turn')]
exec(compile(ast.Module(body=reused,type_ignores=[]),'<cursor-vector-core>','exec'),globals())

def energy(v,a,b,width=4,color=CYAN):
    v.line([a,b],color+'38',width+5)
    v.line([a,b],NAVY,width+2.5)
    v.line([a,b],color,width)
    v.line([a,b],WHITE,max(.9,width*.42))

def saber(v,tip=(8,6),angle=47,scale=1,color=CYAN):
    a=math.radians(angle);d=(math.cos(a),math.sin(a));p=(-d[1],d[0])
    def pt(t,w=0): return (tip[0]+scale*(d[0]*t+p[0]*w),tip[1]+scale*(d[1]*t+p[1]*w))
    v.line([pt(4),pt(39)],color+'40',9*scale)
    v.polygon([pt(0),pt(4,-2.5),pt(39,-2.5),pt(39,2.5),pt(4,2.5)],color,NAVY,1.5*scale)
    v.line([pt(4.5),pt(38)],WHITE,1.9*scale)
    v.polygon([pt(39,-4.6),pt(42,-4.6),pt(42,4.6),pt(39,4.6)],SILVER,NAVY,1.4*scale)
    v.polygon([pt(42,-3),pt(54,-3),pt(54,3),pt(42,3)],SILVER,NAVY,1.6*scale)
    for t in (45,48,51):v.line([pt(t,-2.1),pt(t,2.1)],'#36546A',1.3*scale)
    v.line([pt(54,-2.5),pt(54,2.5)],WHITE,1.4*scale)
    x,y=pt(43.5,2.6);r=1.1*scale
    v.ellipse((x-r,y-r,x+r,y+r),'#F1D6A4',None,0)

def ring(v,frame,center=(32,32),radius=17):
    phase=frame/2
    for i in range(12):
        distance=(i-phase)%12
        color=CYAN if distance<1.3 else '#9DBDD0' if distance<2.8 else '#32546D'
        v.arc(center,radius,i*30-90+4,i*30-90+21,NAVY,5)
        v.arc(center,radius,i*30-90+4,i*30-90+21,color,2.4)

def resize(v,angle):
    def rotated_saber(tip,a):
        tip=turn([tip],angle)[0]
        saber(v,tip,a+angle,.43)
    rotated_saber((32,8),90)
    rotated_saber((32,56),-90)
    v.polygon(turn([(29,28),(35,28),(35,36),(29,36)],angle),SILVER,NAVY,1.6)
    v.line(turn([(29,32),(35,32)],angle),'#4B7088',1.4)

def design(role,frame=0):
    v=Vector()
    if role in ('Arrow','Help','AppStarting','Hand','Pin','Person'):
        saber(v,scale=.83 if role!='Arrow' else 1)
        if role=='AppStarting':ring(v,frame,(47,47),10)
        elif role=='Help':
            pts=[(40,13),(43,10),(49,10),(52,13),(52,18),(46,23),(46,28)]
            v.line(pts,NAVY,5.5);v.line(pts,CYAN,2.4)
            v.ellipse((44,33,48,37),CYAN,NAVY,1.2)
        elif role=='Hand':
            v.ellipse((38,12,49,23),None,NAVY,5)
            v.ellipse((45,19,56,30),None,NAVY,5)
            v.ellipse((38,12,49,23),None,'#DDECF4',2.4)
            v.ellipse((45,19,56,30),None,CYAN,2.4)
        elif role=='Pin':
            v.polygon([(46,30),(39,36),(39,42),(46,54),(53,42),(53,36)],CYAN,NAVY,1.8)
            v.ellipse((43,34,49,40),NAVY,NAVY,1)
        elif role=='Person':
            v.ellipse((43,30,53,40),SILVER,NAVY,1.8)
            v.polygon([(48,42),(40,46),(39,54),(57,54),(56,46)],CYAN,NAVY,1.8)
        return v,(8,6)
    if role=='Wait':
        ring(v,frame,radius=23)
        a=frame*15
        for offset,color in ((0,CYAN),(180,'#B5D6FF')):
            theta=math.radians(a+offset)
            tip=(32+21*math.cos(theta),32+21*math.sin(theta))
            saber(v,tip,a+offset+180,.36,color)
        v.ellipse((29,29,35,35),SILVER,NAVY,1.2)
    elif role=='IBeam':
        energy(v,(32,11),(32,49),3.0)
        for pts in [[(26,10),(38,10)],[(26,54),(38,54)]]:
            v.line(pts,NAVY,5);v.line(pts,SILVER,1.7)
        v.polygon([(29,45),(35,45),(35,52),(29,52)],SILVER,NAVY,1.3)
        v.line([(29,48),(35,48)],'#36546A',1)
    elif role=='Crosshair':
        for a,b in [((32,11),(32,25)),((32,39),(32,53)),((11,32),(25,32)),((39,32),(53,32))]:energy(v,a,b,2.4)
        v.ellipse((29,29,35,35),None,CYAN,1.2)
    elif role=='NWPen':saber(v,(12,52),-47,.97);return v,(12,52)
    elif role=='No':
        v.ellipse((11,11,53,53),None,NAVY,6)
        v.ellipse((11,11,53,53),None,'#E39BA4',2.2)
        energy(v,(19,19),(45,45),3.0,'#FF9BA9')
        energy(v,(45,19),(19,45),3.0,'#FF9BA9')
    elif role.startswith('Size'):
        if role=='SizeAll':resize(v,0);resize(v,90);v.ellipse((29,29,35,35),CYAN,NAVY,1.2)
        else:resize(v,dict(SizeNS=0,SizeWE=90,SizeNWSE=-45,SizeNESW=45)[role])
    elif role=='UpArrow':saber(v,(32,6),90,.95);return v,(32,6)
    return v,(32,32)

def ani(role):
    frames=[]
    for f in range(24):
        v,hotspot=design(role,f)
        frames.append(chunk(b'icon',cur(v,hotspot,(32,48,64,96))))
    header=struct.pack('<9I',36,24,24,0,0,32,1,3,1)
    payload=b'ACON'+chunk(b'anih',header)+chunk(b'LIST',b'fram'+b''.join(frames))
    return b'RIFF'+struct.pack('<I',len(payload))+payload

export=base[base.index('ROLES='):]
export=export.replace('银翼光标','小光剑光标').replace('银白主体 · 冰蓝点缀 · 深色轮廓','冰蓝光刃 · 金属剑柄 · 剑尖点击')
export=export.replace('frames=12 if','frames=24 if').replace('for f in range(12):','for f in range(24):').replace('duration=67','duration=50').replace("data.count(b'icon')==12","data.count(b'icon')==24")
exec(compile(export,'<cursor-asset-export>','exec'),globals())
