import Foundation
import AppKit
import AVFoundation
let args=CommandLine.arguments
let asset=AVURLAsset(url:URL(fileURLWithPath:args[1]));let generator=AVAssetImageGenerator(asset:asset);generator.appliesPreferredTrackTransform=true
let out=URL(fileURLWithPath:args[2]);try FileManager.default.createDirectory(at:out,withIntermediateDirectories:true)
for sec in [1,4,7,10,12,15,19,20]{let cg=try generator.copyCGImage(at:CMTime(value:Int64(sec),timescale:1),actualTime:nil);let rep=NSBitmapImageRep(cgImage:cg);try rep.representation(using:.png,properties:[:])!.write(to:out.appendingPathComponent("video-\(sec)s.png"))}
print("VIDEO_DECODE_PASS duration=\(CMTimeGetSeconds(asset.duration)) seconds; 8 decoded frames")
