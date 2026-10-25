import Foundation
import AppKit
import AVFoundation
let a=CommandLine.arguments
let asset=AVURLAsset(url:URL(fileURLWithPath:a[1]))
let duration=CMTimeGetSeconds(asset.duration)
let tracks=asset.tracks(withMediaType:.video)
print("duration=\(duration) tracks=\(tracks.map{ "\($0.naturalSize) fps=\($0.nominalFrameRate)" })")
let out=URL(fileURLWithPath:a[2]);try FileManager.default.createDirectory(at:out,withIntermediateDirectories:true)
let generator=AVAssetImageGenerator(asset:asset);generator.appliesPreferredTrackTransform=true
generator.requestedTimeToleranceBefore = .zero; generator.requestedTimeToleranceAfter = .zero
let start=a.count>3 ? Double(a[3])! : 0.0
let end=a.count>4 ? min(Double(a[4])!,duration-0.05) : duration-0.05
let step=a.count>5 ? Double(a[5])! : max(2.0,duration/40.0)
generator.maximumSize=CGSize(width:1280,height:1280)
for sec in stride(from:start,through:end,by:step){
 do {
 var actual=CMTime.zero
 let cg=try generator.copyCGImage(at:CMTime(seconds:sec,preferredTimescale:60000),actualTime:&actual)
 let rep=NSBitmapImageRep(cgImage:cg)
 let name=String(format:"%09.3f.png",sec)
 try rep.representation(using:.png,properties:[:])!.write(to:out.appendingPathComponent(name))
 print("\(name) actual=\(CMTimeGetSeconds(actual))")
 }catch{ print("error \(sec): \(error)") }
}
