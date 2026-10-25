import Foundation
import AppKit
import AVFoundation
let args=CommandLine.arguments
let root=URL(fileURLWithPath:args[1]),output=URL(fileURLWithPath:args[2])
let actions=args.count>3 ? Array(args.dropFirst(3)) : ["idle","walk","run","crouch","crawl","crawl","prone-aim","transition"]
var frames:[(URL,String)]=[]
for action in actions {
 let files=try FileManager.default.contentsOfDirectory(at:root.appendingPathComponent(action),includingPropertiesForKeys:nil).filter{$0.pathExtension=="png"}.sorted{$0.lastPathComponent<$1.lastPathComponent}
 if files.count==1 {for _ in 0..<45 {frames.append((files[0],action))}} else {frames+=files.map{($0,action)}}
}
if FileManager.default.fileExists(atPath:output.path){try FileManager.default.removeItem(at:output)}
let writer=try AVAssetWriter(outputURL:output,fileType:.mp4)
let width=960,height=640
let input=AVAssetWriterInput(mediaType:.video,outputSettings:[AVVideoCodecKey:AVVideoCodecType.h264,AVVideoWidthKey:width,AVVideoHeightKey:height,AVVideoCompressionPropertiesKey:[AVVideoAverageBitRateKey:4000000]])
input.expectsMediaDataInRealTime=false
let adaptor=AVAssetWriterInputPixelBufferAdaptor(assetWriterInput:input,sourcePixelBufferAttributes:[kCVPixelBufferPixelFormatTypeKey as String:kCVPixelFormatType_32ARGB,kCVPixelBufferWidthKey as String:width,kCVPixelBufferHeightKey as String:height,kCVPixelBufferCGImageCompatibilityKey as String:true,kCVPixelBufferCGBitmapContextCompatibilityKey as String:true])
writer.add(input);writer.startWriting();writer.startSession(atSourceTime:.zero)
for (index,frame) in frames.enumerated(){
 while !input.isReadyForMoreMediaData{Thread.sleep(forTimeInterval:0.002)}
 autoreleasepool {
  var buffer:CVPixelBuffer?;CVPixelBufferPoolCreatePixelBuffer(nil,adaptor.pixelBufferPool!,&buffer);let pixel=buffer!
  CVPixelBufferLockBaseAddress(pixel,[])
  let context=CGContext(data:CVPixelBufferGetBaseAddress(pixel),width:width,height:height,bitsPerComponent:8,bytesPerRow:CVPixelBufferGetBytesPerRow(pixel),space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.noneSkipFirst.rawValue)!
  let img=NSImage(contentsOf:frame.0)!;var rect=CGRect(x:0,y:0,width:width,height:height);let cg=img.cgImage(forProposedRect:&rect,context:nil,hints:nil)!
  context.draw(cg,in:rect)
  NSGraphicsContext.saveGraphicsState();NSGraphicsContext.current=NSGraphicsContext(cgContext:context,flipped:false)
  let label="IRONFRONT  |  \(frame.1.uppercased())  |  30 FPS"
  (label as NSString).draw(at:NSPoint(x:22,y:height-42),withAttributes:[.font:NSFont.monospacedSystemFont(ofSize:19,weight:.semibold),.foregroundColor:NSColor.white])
  NSGraphicsContext.restoreGraphicsState();CVPixelBufferUnlockBaseAddress(pixel,[])
  if !adaptor.append(pixel,withPresentationTime:CMTime(value:Int64(index),timescale:30)){fatalError("append: \(String(describing:writer.error))")}
 }
}
input.markAsFinished();let done=DispatchSemaphore(value:0);writer.finishWriting{done.signal()};done.wait()
if writer.status != .completed{fatalError("encode: \(String(describing:writer.error))")}
print("ENCODE_PASS frames=\(frames.count) duration=\(Double(frames.count)/30) output=\(output.path)")
