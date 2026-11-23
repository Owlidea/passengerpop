// Renders the Google Play feature graphic (1024x500, opaque PNG) from the game's own art:
// the Central Line station as background, the Passenger Pop logo in the middle, the waving conductor
// on the left and a row of trains on the right.
// Usage: swift Tools/art/play_feature_graphic.swift <repo root> <output.png>
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

let args = CommandLine.arguments
guard args.count == 3 else {
    print("usage: swift play_feature_graphic.swift <repo root> <output.png>")
    exit(1)
}
let root = args[1]
let output = args[2]
let width = 1024, height = 500
let W = Double(width), H = Double(height)
let space = CGColorSpace(name: CGColorSpace.sRGB)!
// No alpha channel: Google Play rejects feature graphics with transparency.
let ctx = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
                    space: space, bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
ctx.interpolationQuality = .high

func load(_ relative: String) -> CGImage {
    let url = URL(fileURLWithPath: root + "/" + relative) as CFURL
    guard let source = CGImageSourceCreateWithURL(url, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        print("cannot read " + relative)
        exit(1)
    }
    return image
}

func color(_ r: Double, _ g: Double, _ b: Double, _ a: Double) -> CGColor {
    CGColor(srgbRed: r, green: g, blue: b, alpha: a)
}

/// Draws with a top-left origin, like the layout numbers below.
func draw(_ image: CGImage, x: Double, top: Double, width w: Double, height h: Double,
          shadow: Double = 0, mirrored: Bool = false) {
    let rect = CGRect(x: x, y: H - top - h, width: w, height: h)
    ctx.saveGState()
    if shadow > 0 { ctx.setShadow(offset: CGSize(width: 0, height: -shadow * 0.35), blur: shadow, color: color(0, 0, 0, 0.45)) }
    if mirrored {
        ctx.translateBy(x: rect.midX, y: 0)
        ctx.scaleBy(x: -1, y: 1)
        ctx.translateBy(x: -rect.midX, y: 0)
    }
    ctx.draw(image, in: rect)
    ctx.restoreGState()
}

// Background: the station, scaled to the full width and shifted so the arches, lamps and rails stay in view.
let station = load("MetroSort-Media/Gorseller/central-line.png")
let stationHeight = W * Double(station.height) / Double(station.width)
draw(station, x: 0, top: -30, width: W, height: stationHeight)

// Darken the edges a little so the logo and characters stand out.
let shade = CGGradient(colorsSpace: space, colors: [color(0.02, 0.10, 0.13, 0.0), color(0.02, 0.10, 0.13, 0.55)] as CFArray,
                       locations: [0, 1])!
ctx.drawLinearGradient(shade, start: CGPoint(x: 0, y: H * 0.6), end: CGPoint(x: 0, y: 0), options: [])
let topShade = CGGradient(colorsSpace: space, colors: [color(0.02, 0.10, 0.13, 0.0), color(0.02, 0.10, 0.13, 0.35)] as CFArray,
                          locations: [0, 1])!
ctx.drawLinearGradient(topShade, start: CGPoint(x: 0, y: H * 0.75), end: CGPoint(x: 0, y: H), options: [])
// Warm glow behind the logo.
let glow = CGGradient(colorsSpace: space, colors: [color(1.0, 0.92, 0.65, 0.55), color(1.0, 0.92, 0.65, 0.0)] as CFArray,
                      locations: [0, 1])!
ctx.drawRadialGradient(glow, startCenter: CGPoint(x: W / 2, y: H / 2), startRadius: 0,
                       endCenter: CGPoint(x: W / 2, y: H / 2), endRadius: 330, options: [])

// Trains on the right, the nearest one largest.
let trainLayout: [(index: Int, x: Double, top: Double, size: Double)] = [
    (3, 905, 250, 112), (1, 840, 290, 138), (0, 770, 330, 165),
]
for t in trainLayout {
    let train = load("Assets/MetroSort/Resources/Art/train_icon_\(t.index).png")
    let h = t.size * Double(train.height) / Double(train.width)
    draw(train, x: t.x, top: t.top, width: t.size, height: h, shadow: 14)
}

// The conductor waves from the left.
let conductor = load("Assets/MetroSort/Resources/Art/conductor_wave.png")
let conductorHeight = 350.0
let conductorWidth = conductorHeight * Double(conductor.width) / Double(conductor.height)
draw(conductor, x: 52, top: H - conductorHeight - 8, width: conductorWidth, height: conductorHeight, shadow: 18)

// The logo in the middle (inside Google's safe area for cropped placements).
let logo = load("MetroSort-Media/Gorseller/passenger-pop-logo.png")
let logoWidth = 500.0
let logoHeight = logoWidth * Double(logo.height) / Double(logo.width)
draw(logo, x: (W - logoWidth) / 2, top: (H - logoHeight) / 2 - 4, width: logoWidth, height: logoHeight, shadow: 22)

guard let image = ctx.makeImage(),
      let destination = CGImageDestinationCreateWithURL(URL(fileURLWithPath: output) as CFURL,
                                                        UTType.png.identifier as CFString, 1, nil) else {
    print("cannot write " + output)
    exit(1)
}
CGImageDestinationAddImage(destination, image, nil)
guard CGImageDestinationFinalize(destination) else {
    print("cannot write " + output)
    exit(1)
}
print("written " + output)
