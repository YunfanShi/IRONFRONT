import Foundation
import AppKit
import AVFoundation

let args = CommandLine.arguments
let root = URL(fileURLWithPath: args[1])
let output = URL(fileURLWithPath: args[2])
let frames = try FileManager.default.contentsOfDirectory(at: root, includingPropertiesForKeys: nil)
    .filter { $0.pathExtension == "png" }.sorted { $0.lastPathComponent < $1.lastPathComponent }
let expectedFrames = args.count > 5 ? Int(args[5])! : 336
guard frames.count == expectedFrames else { fatalError("Expected \(expectedFrames) comparison frames") }
if FileManager.default.fileExists(atPath: output.path) { try FileManager.default.removeItem(at: output) }
let width = args.count > 3 ? Int(args[3])! : 1920
let height = args.count > 4 ? Int(args[4])! : 800
let writer = try AVAssetWriter(outputURL: output, fileType: .mp4)
writer.shouldOptimizeForNetworkUse = true
let input = AVAssetWriterInput(mediaType: .video, outputSettings: [
    AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: width, AVVideoHeightKey: height,
    AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 8000000*width/1920]])
input.expectsMediaDataInRealTime = false
let adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [
    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32ARGB,
    kCVPixelBufferWidthKey as String: width, kCVPixelBufferHeightKey as String: height,
    kCVPixelBufferCGImageCompatibilityKey as String: true,
    kCVPixelBufferCGBitmapContextCompatibilityKey as String: true])
writer.add(input)
guard writer.startWriting() else { fatalError("Unable to start encoder: \(String(describing: writer.error))") }
writer.startSession(atSourceTime: .zero)
for (index, frame) in frames.enumerated() {
    while !input.isReadyForMoreMediaData { Thread.sleep(forTimeInterval: 0.002) }
    autoreleasepool {
        guard let pool = adaptor.pixelBufferPool else { fatalError("No pixel buffer pool") }
        var buffer: CVPixelBuffer?
        let status = CVPixelBufferPoolCreatePixelBuffer(nil, pool, &buffer)
        guard status == kCVReturnSuccess, let pixel = buffer else { fatalError("Pixel buffer allocation failed: \(status)") }
        CVPixelBufferLockBaseAddress(pixel, [])
        let context = CGContext(data: CVPixelBufferGetBaseAddress(pixel), width: width, height: height,
            bitsPerComponent: 8, bytesPerRow: CVPixelBufferGetBytesPerRow(pixel),
            space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue)!
        let img = NSImage(contentsOf: frame)!
        var rect = CGRect(x: 0, y: 0, width: width, height: height)
        context.draw(img.cgImage(forProposedRect: &rect, context: nil, hints: nil)!, in: rect)
        CVPixelBufferUnlockBaseAddress(pixel, [])
        guard adaptor.append(pixel, withPresentationTime: CMTime(value: Int64(index), timescale: 30)) else {
            fatalError("Append failed: \(String(describing: writer.error))")
        }
    }
}
input.markAsFinished()
let done = DispatchSemaphore(value: 0)
writer.finishWriting { done.signal() }
done.wait()
guard writer.status == .completed else { fatalError("Encode failed: \(String(describing: writer.error))") }
print("ENCODE_PASS frames=\(frames.count) duration=\(Double(frames.count)/30) fps=30 size=\(width)x\(height) output=\(output.path)")
