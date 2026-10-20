using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace RR.Game
{
    // Records real player frames and elapsed wall time. File I/O affects capture rate, never simulated time.
    public sealed class MotionCapture : MonoBehaviour
    {
        public static int Active {get;private set;}
        public static void Begin(GameRoot root,string directory,string clip,float seconds)
        {
            if(!Args.Has("-rrMotionRecord"))return;
            var go=new GameObject("Capture / "+clip);go.transform.SetParent(root.transform,false);
            go.AddComponent<MotionCapture>().StartCoroutine(Record(go,Path.Combine(directory,"clips",clip),seconds));
        }
        static IEnumerator Record(GameObject owner,string dir,float duration)
        {
            Directory.CreateDirectory(dir);Active++;
            var rows=new StringBuilder("frame,seconds\n");float start=Time.unscaledTime;int i=0;
            try
            {
                while(Time.unscaledTime-start<duration)
                {
                    yield return new WaitForEndOfFrame();
                    string name="frame_"+(i++).ToString("D4")+".png";
                    rows.AppendLine(name+","+(Time.unscaledTime-start).ToString("0.000000",System.Globalization.CultureInfo.InvariantCulture));
                    var texture=UnitReviewCapture.Frame();
                    try { File.WriteAllBytes(Path.Combine(dir,name),texture.EncodeToPNG()); }
                    finally { Object.Destroy(texture); }
                    yield return new WaitForSecondsRealtime(.025f);
                }
            }
            finally { File.WriteAllText(Path.Combine(dir,"timings.csv"),rows.ToString());Active--;Object.Destroy(owner); }
        }
    }
}
