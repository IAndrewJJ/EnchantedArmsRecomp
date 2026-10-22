"""Encode the approved source PNG as a multi-resolution Windows icon. Developer-only; requires Pillow."""
from pathlib import Path


def main():
    from PIL import Image
    folder=Path(__file__).resolve().parents[1]/'launcher/branding'
    sizes=[(n,n) for n in (16,20,24,32,40,48,64,96,128,256)]
    with Image.open(folder/'EnchantedArmsPC.png') as source:
        if source.width!=source.height or source.mode!='RGBA' or source.getextrema()[3][0]!=0:
            raise ValueError('The approved icon must be square with a transparent alpha channel')
        source.save(folder/'EnchantedArmsPC.ico',format='ICO',sizes=sizes)
    with Image.open(folder/'EnchantedArmsPC.ico') as result:
        if result.ico.sizes()!=set(sizes):
            raise ValueError('Windows icon resolution frames are incomplete')
    print('Encoded 10 Windows icon sizes, 16 through 256 pixels')


if __name__=='__main__':main()
