using System;

namespace EmergencyVR.Medical.Interaction
{
    // Game-input travel thresholds, not measurements of clinical force or manoeuvre quality.
    public sealed class ChokingGestureEvaluator
    {
        bool ready, pressed;
        double started, lastTime = double.NegativeInfinity;

        public bool Sample(double time, double travel, bool placementValid)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < lastTime ||
                double.IsNaN(travel) || double.IsInfinity(travel) || !placementValid)
            { Cancel(); return false; }
            lastTime = time;
            if (travel > .09 || travel < -.025) { Cancel(); return false; }
            if (!ready)
            {
                if (travel <= .008) { ready = true; started = time; }
                return false;
            }
            if (time - started > 3) { Cancel(); return false; }
            if (travel >= .035) pressed = true;
            if (!pressed || travel > .008) return false;
            bool completed = time - started >= .08;
            ready = pressed = false;
            return completed;
        }

        public void Cancel() { ready = pressed = false; lastTime = double.NegativeInfinity; }
    }
}
