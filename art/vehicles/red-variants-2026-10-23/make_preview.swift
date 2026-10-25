import Foundation
import CoreGraphics
import CoreText
import ImageIO

let root=URL(fileURLWithPath:CommandLine.arguments[1])
let width=1536, height=770
let cs=CGColorSpaceCreateDeviceRGB()
let ctx=CGContext(data:nil,width:width,height:height,bitsPerComponent:8,bytesPerRow:width*4,space:cs,bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.setFillColor(CGColor(red:0.13,green:0.15,blue:0.18,alpha:1));ctx.fill(CGRect(x:0,y:0,width:width,height:height))
func text(_ s:String,_ x:CGFloat,_ y:CGFloat,_ size:CGFloat){
    let attrs:[NSAttributedString.Key:Any]=[NSAttributedString.Key(kCTFontAttributeName as String):CTFontCreateWithName("Helvetica" as CFString,size,nil),NSAttributedString.Key(kCTForegroundColorAttributeName as String):CGColor(gray:0.93,alpha:1)]
    let line=CTLineCreateWithAttributedString(NSAttributedString(string:s,attributes:attrs));ctx.textPosition=CGPoint(x:x,y:y);CTLineDraw(line,ctx)
}
text("IRONFRONT  /  RED FACTION",24,729,25)
let items=[("T90 BASTION","t90-bastion"),("V12 LANCER","v12-lancer"),("R4 SCOUT","r4-scout"),("U8 ROVER","u8-rover"),("M2 COURIER","m2-courier"),("R3 FIREFLY","r3-firefly"),("J20 STRATUS","j20-stratus")]
for (i,item) in items.enumerated(){
    let x=CGFloat(i%4)*384, y=CGFloat(1-i/4)*350
    let url=root.appendingPathComponent(item.1+"/red-preview/perspective.png")
    let source=CGImageSourceCreateWithURL(url as CFURL,nil)!
    let image=CGImageSourceCreateImageAtIndex(source,0,nil)!
    ctx.draw(image,in:CGRect(x:x+6,y:y+38,width:372,height:302.25))
    text(item.0,x+18,y+14,18)
}
let output=root.appendingPathComponent("red-variants-2026-10-23/preview.png")
let destination=CGImageDestinationCreateWithURL(output as CFURL,"public.png" as CFString,1,nil)!
CGImageDestinationAddImage(destination,ctx.makeImage()!,nil)
guard CGImageDestinationFinalize(destination) else {fatalError("Preview save failed")}
print(output.path)
