import Foundation
import AppKit
import AVFoundation

let args=CommandLine.arguments
let asset=AVURLAsset(url:URL(fileURLWithPath:args[1]))
let generator=AVAssetImageGenerator(asset:asset)
generator.appliesPreferredTrackTransform=true
generator.requestedTimeToleranceBefore = .zero
generator.requestedTimeToleranceAfter = .zero
let output=URL(fileURLWithPath:args[2])
try FileManager.default.createDirectory(at:output,withIntermediateDirectories:true)
for sec in [2.6,3.0,3.5,4.1,8.1,8.4,8.7,9.1] {
    var actual=CMTime.zero
    let cg=try generator.copyCGImage(at:CMTime(seconds:sec,preferredTimescale:600),actualTime:&actual)
    guard abs(CMTimeGetSeconds(actual)-sec)<0.001 else { fatalError("Wrong sample time") }
    let image=NSBitmapImageRep(cgImage:cg)
    try image.representation(using:.png,properties:[:])!.write(to:output.appendingPathComponent("video-\(sec)s.png"))
    print("FRAME requested=\(sec) actual=\(CMTimeGetSeconds(actual))")
}
let track=asset.tracks(withMediaType:.video).first!
print("VIDEO_DECODE_PASS duration=\(CMTimeGetSeconds(asset.duration)) fps=\(track.nominalFrameRate) size=\(track.naturalSize); 8 exact-time frames")
