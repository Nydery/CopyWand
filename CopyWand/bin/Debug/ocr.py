import sys
import pytesseract
pytesseract.pytesseract.tesseract_cmd = r'C:\Users\offic\AppData\Local\Tesseract-OCR\tesseract.exe'

from PIL import Image

path = sys.argv[1]

img = Image.open(path)

text = pytesseract.image_to_string(img, lang='')
if text != '':
    print(text)
   
else: 
    print('ERROR')