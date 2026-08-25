from pathlib import Path
import sys
import numpy as np
from PIL import Image, ImageFilter

for name in sys.argv[1:]:
    im = Image.open(name).convert('RGB')
    a = np.asarray(im).astype(np.float32)/255
    mx, mn = a.max(2), a.min(2)
    sat = mx-mn
    # light neutral background is rejected; colored/dark illustration remains.
    mask = ((sat > .10) | (mx < .72)) & ~((mx > .82) & (sat < .09))
    m = Image.fromarray((mask*255).astype('uint8')).filter(ImageFilter.MaxFilter(31))
    arr=np.asarray(m)>0
    h,w=arr.shape; seen=np.zeros_like(arr); boxes=[]
    for y in range(h):
      for x in range(w):
        if not arr[y,x] or seen[y,x]: continue
        st=[(y,x)]; seen[y,x]=1; xs=[]; ys=[]
        while st:
          yy,xx=st.pop(); xs.append(xx); ys.append(yy)
          for ny,nx in ((yy-1,xx),(yy+1,xx),(yy,xx-1),(yy,xx+1)):
            if 0<=ny<h and 0<=nx<w and arr[ny,nx] and not seen[ny,nx]: seen[ny,nx]=1; st.append((ny,nx))
        if len(xs)>1000: boxes.append((min(xs),min(ys),max(xs)+1,max(ys)+1,len(xs)))
    print(Path(name).name, im.size, len(boxes), sorted(boxes,key=lambda b:(b[1],b[0]))[:20])
