import com.android.apksig.ApkSigner;
import com.android.apksig.ApkVerifier;

import java.io.File;
import java.io.FileInputStream;
import java.security.KeyStore;
import java.security.PrivateKey;
import java.security.cert.Certificate;
import java.security.cert.X509Certificate;
import java.util.ArrayList;
import java.util.List;

/** Firma un APK (esquema v2: basta desde Android 7) con una llave PKCS12 y lo verifica. Uso: Firma entrada.apk salida.apk llave.p12 clave alias minSdk */
public class Firma {
    public static void main(String[] a) throws Exception {
        KeyStore ks = KeyStore.getInstance("PKCS12");
        FileInputStream f = new FileInputStream(a[2]);
        ks.load(f, a[3].toCharArray());
        f.close();
        PrivateKey k = (PrivateKey) ks.getKey(a[4], a[3].toCharArray());
        List<X509Certificate> cadena = new ArrayList<X509Certificate>();
        for (Certificate c : ks.getCertificateChain(a[4])) cadena.add((X509Certificate) c);
        ApkSigner.SignerConfig sc = new ApkSigner.SignerConfig.Builder("rocola", k, cadena).build();
        List<ApkSigner.SignerConfig> sig = new ArrayList<ApkSigner.SignerConfig>();
        sig.add(sc);
        new ApkSigner.Builder(sig).setInputApk(new File(a[0])).setOutputApk(new File(a[1]))
            .setMinSdkVersion(Integer.parseInt(a[5])).setV1SigningEnabled(false).setV2SigningEnabled(true).build().sign();
        ApkVerifier.Result r = new ApkVerifier.Builder(new File(a[1])).setMinCheckedPlatformVersion(Integer.parseInt(a[5])).build().verify();
        System.out.println("Firma verificada: " + r.isVerified() + " (v1=" + r.isVerifiedUsingV1Scheme() + ", v2=" + r.isVerifiedUsingV2Scheme() + ")");
        if (!r.isVerified()) {
            for (Object e : r.getErrors()) System.out.println("  error: " + e);
            System.exit(1);
        }
    }
}
