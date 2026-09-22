// Rasterize the official vendor font. This does not draw replacement icon shapes.
// NODE_PATH must contain Playwright; optional FLATICON_BROWSER selects Chromium.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../../..');
const sourcePath=path.join(root,'Assets/Art/UI/Flaticon/Recipes/Sources.json');
const source=JSON.parse(fs.readFileSync(sourcePath,'utf8'));
function meta(file){
 const dest=file+'.meta'; if(fs.existsSync(dest))return;
 const guid=crypto.randomUUID().replaceAll('-','');
 fs.writeFileSync(dest,`fileFormatVersion: 2\nguid: ${guid}\nTextureImporter:\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n  isReadable: 0\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  textureType: 8\n  textureShape: 1\n  spriteMode: 1\n  spriteMeshType: 0\n  spritePixelsToUnits: 100\n  spritePivot: {x: 0.5, y: 0.5}\n  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  alphaIsTransparency: 1\n  platformSettings:\n  - serializedVersion: 3\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 512\n    textureFormat: -1\n    textureCompression: 0\n    overridden: 0\n`);
}
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.FLATICON_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage();
  const fontData=fs.readFileSync(path.join(root,source.font)).toString('base64');
  await page.evaluate(async data=>{
   const font=new FontFace('FlaticonSource',`url(data:font/woff;base64,${data})`);
   await font.load();document.fonts.add(font);
  },fontData);
  for(const icon of source.icons.filter(i=>i.codepoint)){
   const data=await page.evaluate(code=>{
    const canvas=document.createElement('canvas');canvas.width=512;canvas.height=512;
    const c=canvas.getContext('2d'),glyph=String.fromCodePoint(code);
    c.font='448px FlaticonSource';c.fillStyle='white';c.textBaseline='alphabetic';
    const m=c.measureText(glyph);
    const w=m.actualBoundingBoxLeft+m.actualBoundingBoxRight,h=m.actualBoundingBoxAscent+m.actualBoundingBoxDescent;
    if(w<=0||h<=0||w>512||h>512)throw new Error('Unexpected vendor glyph bounds');
    c.fillText(glyph,(512-w)/2+m.actualBoundingBoxLeft,(512-h)/2+m.actualBoundingBoxAscent);
    return canvas.toDataURL('image/png').split(',')[1];
   },icon.codepoint);
   const dest=path.join(root,icon.path);fs.writeFileSync(dest,Buffer.from(data,'base64'));meta(dest);
  }
  // Contact sheet from actual assets; runtime tint only, never change shared originals.
  await page.setViewportSize({width:1200,height:480});
  await page.setContent('<style>body{margin:0;background:#172023;color:#eae8d3;font:17px Consolas,monospace;padding:25px}h1{font-size:25px;font-weight:400;margin:0 0 20px}.grid{display:grid;grid-template-columns:repeat(5,1fr);gap:22px}.icon{height:90px}img{width:42px;height:42px;object-fit:contain;filter:brightness(0) invert(1)}small{display:block;color:#b5c4c0;font-size:12px;margin-top:4px}</style><h1>RECETAS / ICONOS EXISTENTES + FLATICON</h1><div class="grid"></div>');
  const items=source.icons.map(i=>({...i,image:fs.readFileSync(path.join(root,i.path)).toString('base64')}));
  await page.evaluate(items=>{
   const grid=document.querySelector('.grid');
   for(const item of items){const cell=document.createElement('div');cell.className='icon';const img=document.createElement('img');img.src='data:image/png;base64,'+item.image;cell.append(img);const label=document.createElement('div');label.textContent=item.key.replace('Icon','');cell.append(label);const note=document.createElement('small');note.textContent=item.codepoint?'Flaticon / Uicons':'Reutilizado del proyecto';cell.append(note);grid.append(cell);}
  },items);
  await page.locator('img').evaluateAll(imgs=>Promise.all(imgs.map(i=>i.decode())));
  await page.screenshot({path:path.join(root,'Docs/Previews/RecipeUIIcons.png'),fullPage:true});
  console.log('FLATICON_ICONS_RENDERED: 8 official glyphs; 7 existing icons retained.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
