from pathlib import Path
from pypdf import PdfReader
from pdf2image import convert_from_path
import json

base = Path(r'C:\Users\elian\Mismo\output\gdd')
pdf = base/'Mismo_GDD_v0.9-qa.pdf'
out = base/'qa_final'
out.mkdir(exist_ok=True)
poppler = r'C:\Users\elian\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler\Library\bin'
pages = convert_from_path(str(pdf),dpi=110,poppler_path=poppler,thread_count=2)
reader=PdfReader(pdf)
report=[]
for i,image in enumerate(pages):
    image.save(out/f'page-{i+1:02}.png')
    lines=[t for t in reader.pages[i].extract_text().splitlines() if t.strip()]
    info={'page':i+1,'lines':len(lines),'start':lines[:2],'end':lines[-2:]}
    report.append(info)
    print(json.dumps(info,ensure_ascii=False))
(out/'page-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
