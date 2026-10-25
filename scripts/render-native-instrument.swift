import Foundation
import AVFoundation
import AudioToolbox

// Offline performance of original note events through macOS's installed GM bank.
// The bank itself is never copied into the project or distribution.
struct Note: Decodable { let pitch: UInt8; let velocity: UInt8; let start: Double; let duration: Double }
struct Track: Decodable { let name: String; let program: UInt8; let volume: Float; let pan: Float; let notes: [Note] }
struct Score: Decodable { let bpm: Double; let beats: Double; let sampleRate: Double; let tracks: [Track] }
struct Event { let frame: AVAudioFramePosition; let track: Int; let pitch: UInt8; let velocity: UInt8; let on: Bool }

guard CommandLine.arguments.count == 3 else { fatalError("usage: renderer score.json stereo-float32.raw") }
let score = try JSONDecoder().decode(Score.self, from: Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1])))
let engine = AVAudioEngine()
let format = AVAudioFormat(standardFormatWithSampleRate: score.sampleRate, channels: 2)!
var samplers: [AVAudioUnitSampler] = []
var mixers: [AVAudioMixerNode] = []
let bank = URL(fileURLWithPath: "/System/Library/Components/CoreAudio.component/Contents/Resources/gs_instruments.dls")
for track in score.tracks {
    let sampler = AVAudioUnitSampler()
    let mixer = AVAudioMixerNode()
    engine.attach(sampler); engine.attach(mixer)
    try sampler.loadSoundBankInstrument(at: bank, program: track.program,
        bankMSB: UInt8(kAUSampler_DefaultMelodicBankMSB), bankLSB: UInt8(kAUSampler_DefaultBankLSB))
    engine.connect(sampler, to: mixer, format: format)
    engine.connect(mixer, to: engine.mainMixerNode, format: format)
    mixer.outputVolume = track.volume; mixer.pan = track.pan
    samplers.append(sampler); mixers.append(mixer)
}
let loopFrames = AVAudioFramePosition((score.beats * 60 / score.bpm * score.sampleRate).rounded())
var events: [Event] = []
// Three passes retain the second pass, with preceding and following tails for circular DSP.
for pass in 0..<3 {
    for (i, track) in score.tracks.enumerated() {
        for n in track.notes {
            let frame = AVAudioFramePosition((n.start * 60 / score.bpm * score.sampleRate).rounded()) + AVAudioFramePosition(pass) * loopFrames
            let end = frame + AVAudioFramePosition((n.duration * 60 / score.bpm * score.sampleRate).rounded())
            events.append(Event(frame: frame, track: i, pitch: n.pitch, velocity: n.velocity, on: true))
            events.append(Event(frame: end, track: i, pitch: n.pitch, velocity: n.velocity, on: false))
        }
    }
}
events.sort { $0.frame == $1.frame ? (!$0.on && $1.on) : $0.frame < $1.frame }
try engine.enableManualRenderingMode(.offline, format: format, maximumFrameCount: 4096)
try engine.start()
let buffer = AVAudioPCMBuffer(pcmFormat: engine.manualRenderingFormat, frameCapacity: 4096)!
let outputURL = URL(fileURLWithPath: CommandLine.arguments[2])
FileManager.default.createFile(atPath: outputURL.path, contents: nil)
let output = try FileHandle(forWritingTo: outputURL)
var frame: AVAudioFramePosition = 0
var eventIndex = 0
var idle = 0
while frame < 3 * loopFrames {
    while eventIndex < events.count && events[eventIndex].frame <= frame {
        let e = events[eventIndex]
        if e.on { samplers[e.track].startNote(e.pitch, withVelocity: e.velocity, onChannel: 0) }
        else { samplers[e.track].stopNote(e.pitch, onChannel: 0) }
        eventIndex += 1
    }
    let next = eventIndex < events.count ? events[eventIndex].frame : 3 * loopFrames
    let count = AVAudioFrameCount(max(1, min(4096, min(next - frame, 3 * loopFrames - frame))))
    let result = try engine.renderOffline(count, to: buffer)
    if result == .success {
        let length = Int(buffer.frameLength)
        let channels = buffer.floatChannelData!
        var interleaved = [Float](repeating: 0, count: length * 2)
        for i in 0..<length { interleaved[2 * i] = channels[0][i]; interleaved[2 * i + 1] = channels[1][i] }
        try interleaved.withUnsafeBufferPointer { ptr in
            try output.write(contentsOf: Data(bytes: ptr.baseAddress!, count: ptr.count * MemoryLayout<Float>.size))
        }
        frame += AVAudioFramePosition(length); idle = 0
    } else {
        idle += 1
        if idle > 1000 { fatalError("offline audio failed: \(result)") }
    }
}
try output.close()
engine.stop()
print("Rendered \(score.tracks.count) GM instruments, \(Double(loopFrames) / score.sampleRate)s loop x3, stereo Float32.")
